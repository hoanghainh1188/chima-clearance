// Command server is the Go spike backend. It reproduces just enough of the .NET wire
// contract to prove the riskiest unknown of a .NET→Go port: that the UNCHANGED .NET
// SignalR client can connect to a Go SignalR server and receive events.
//
// Scope: /api/admin/health, POST /api/auth/login (RS256), SignalR /hubs/events pushing
// LedgerChanged. Real DB-backed ledger reads are added separately; the SignalR proof
// does not need them.
package main

import (
	"context"
	"encoding/json"
	"log"
	"net/http"
	"strings"
	"time"

	"b2spike/internal/auth"
	"b2spike/internal/hub"

	"github.com/google/uuid"
	"github.com/philippseith/signalr"
	"golang.org/x/crypto/bcrypt"
)

const listenAddr = "127.0.0.1:5000" // matches desktop apiBase http://127.0.0.1:5000/

// testUser is an in-memory account so the SignalR proof runs without a seeded b2.db.
// (The real login path reading Users from b2.db is added in internal/ledger.)
type account struct {
	id           uuid.UUID
	username     string
	passwordHash []byte
	fullName     string
	role         string
	mustChange   bool
}

func main() {
	authSvc, err := auth.New()
	if err != nil {
		log.Fatalf("auth init: %v", err)
	}

	hash, _ := bcrypt.GenerateFromPassword([]byte("admin123"), bcrypt.DefaultCost)
	testUser := account{
		id:           uuid.MustParse("11111111-1111-1111-1111-111111111111"),
		username:     "admin",
		passwordHash: hash,
		fullName:     "Quản trị viên",
		role:         "Admin",
		mustChange:   false,
	}

	ctx := context.Background()
	server, err := signalr.NewServer(ctx,
		signalr.SimpleHubFactory(&hub.AppHub{}),
		signalr.KeepAliveInterval(2*time.Second),
	)
	if err != nil {
		log.Fatalf("signalr server: %v", err)
	}

	// SignalR registers /hubs/events and /hubs/events/negotiate on its own mux.
	sigMux := http.NewServeMux()
	server.MapHTTP(signalr.WithHTTPServeMux(sigMux), "/hubs/events")

	mux := http.NewServeMux()
	mux.HandleFunc("/api/admin/health", handleHealth)
	mux.HandleFunc("/api/auth/login", handleLogin(authSvc, testUser))
	mux.HandleFunc("/debug/push", handleDebugPush(server))
	// Hub paths go through token validation (see hubAuth) then to the SignalR mux.
	mux.Handle("/hubs/events", hubAuth(authSvc, sigMux))
	mux.Handle("/hubs/events/negotiate", hubAuth(authSvc, sigMux))

	// Prove push works even with no manual trigger: broadcast every 4s.
	go func() {
		t := time.NewTicker(4 * time.Second)
		defer t.Stop()
		for range t.C {
			hub.PushLedgerChanged(server.HubClients(), uuid.NewString())
		}
	}()

	log.Printf("[spike] listening on http://%s  (login admin/admin123)", listenAddr)
	if err := http.ListenAndServe(listenAddr, mux); err != nil {
		log.Fatalf("listen: %v", err)
	}
}

func handleHealth(w http.ResponseWriter, _ *http.Request) {
	writeJSON(w, http.StatusOK, map[string]any{
		"status": "OK",
		"uptime": time.Now().UnixMilli(),
	})
}

type loginRequest struct {
	Username string `json:"username"`
	Password string `json:"password"`
}

type loginResponse struct {
	AccessToken          string    `json:"accessToken"`
	RefreshToken         string    `json:"refreshToken"`
	AccessTokenExpiresAt time.Time `json:"accessTokenExpiresAt"`
	MustChangePassword   bool      `json:"mustChangePassword"`
	Role                 string    `json:"role"` // string enum: Admin/Operator/Supervisor
	UserID               string    `json:"userId"`
	FullName             string    `json:"fullName"`
}

func handleLogin(authSvc *auth.Service, u account) http.HandlerFunc {
	return func(w http.ResponseWriter, r *http.Request) {
		if r.Method != http.MethodPost {
			w.WriteHeader(http.StatusMethodNotAllowed)
			return
		}
		var req loginRequest
		if err := json.NewDecoder(r.Body).Decode(&req); err != nil {
			writeJSON(w, http.StatusBadRequest, map[string]string{"code": "INVALID_BODY"})
			return
		}
		if !strings.EqualFold(req.Username, u.username) ||
			bcrypt.CompareHashAndPassword(u.passwordHash, []byte(req.Password)) != nil {
			writeJSON(w, http.StatusUnauthorized, map[string]string{"code": "INVALID_CREDENTIALS"})
			return
		}
		now := time.Now().UTC()
		token, exp, err := authSvc.Issue(u.id, u.role, u.mustChange, now)
		if err != nil {
			writeJSON(w, http.StatusInternalServerError, map[string]string{"code": "TOKEN_ERROR"})
			return
		}
		writeJSON(w, http.StatusOK, loginResponse{
			AccessToken:          token,
			RefreshToken:         uuid.NewString(),
			AccessTokenExpiresAt: exp,
			MustChangePassword:   u.mustChange,
			Role:                 u.role,
			UserID:               u.id.String(),
			FullName:             u.fullName,
		})
	}
}

func handleDebugPush(server signalr.Server) http.HandlerFunc {
	return func(w http.ResponseWriter, _ *http.Request) {
		id := uuid.NewString()
		hub.PushLedgerChanged(server.HubClients(), id)
		writeJSON(w, http.StatusOK, map[string]string{"pushed": id})
	}
}

// hubAuth validates the bearer token the .NET client attaches (Authorization header on
// negotiate, ?access_token= on the WebSocket). Spike leniency: a MISSING token is
// allowed (logged) so an auth-plumbing mismatch cannot mask the SignalR protocol result
// we are trying to measure; a PRESENT-but-invalid token is rejected.
func hubAuth(authSvc *auth.Service, next http.Handler) http.Handler {
	return http.HandlerFunc(func(w http.ResponseWriter, r *http.Request) {
		tok := bearerToken(r)
		if tok == "" {
			log.Printf("[hub-auth] no token on %s (allowed for spike)", r.URL.Path)
			next.ServeHTTP(w, r)
			return
		}
		if _, err := authSvc.Validate(tok); err != nil {
			log.Printf("[hub-auth] invalid token on %s: %v", r.URL.Path, err)
			w.WriteHeader(http.StatusUnauthorized)
			return
		}
		next.ServeHTTP(w, r)
	})
}

func bearerToken(r *http.Request) string {
	if h := r.Header.Get("Authorization"); strings.HasPrefix(h, "Bearer ") {
		return strings.TrimPrefix(h, "Bearer ")
	}
	return r.URL.Query().Get("access_token")
}

func writeJSON(w http.ResponseWriter, status int, body any) {
	w.Header().Set("Content-Type", "application/json")
	w.WriteHeader(status)
	_ = json.NewEncoder(w).Encode(body)
}
