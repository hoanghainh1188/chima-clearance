// Package auth issues and validates RS256 JWTs, mirroring the .NET
// JwtTokenService contract (claims: sub, role, must_change_password; RS256; 8h TTL).
//
// Spike simplification: the server generates its own RSA keypair at startup and both
// issues and validates with it. The .NET server does not validate issuer/audience and
// uses a self-generated key too, so a self-consistent key fully satisfies the flow.
package auth

import (
	"crypto/rand"
	"crypto/rsa"
	"errors"
	"time"

	"github.com/golang-jwt/jwt/v5"
	"github.com/google/uuid"
)

// AccessTokenTTL matches JwtTokenService.AccessTokenHours (8h).
const AccessTokenTTL = 8 * time.Hour

type Service struct {
	key *rsa.PrivateKey
}

func New() (*Service, error) {
	k, err := rsa.GenerateKey(rand.Reader, 2048)
	if err != nil {
		return nil, err
	}
	return &Service{key: k}, nil
}

// Issue returns a signed RS256 token and its expiry. Claim names match the .NET
// contract exactly: sub (user id), role (string), must_change_password ("true"/"false").
func (s *Service) Issue(userID uuid.UUID, role string, mustChange bool, now time.Time) (string, time.Time, error) {
	exp := now.Add(AccessTokenTTL)
	claims := jwt.MapClaims{
		"sub":                  userID.String(),
		"role":                 role,
		"must_change_password": boolStr(mustChange),
		"nbf":                  now.Unix(),
		"exp":                  exp.Unix(),
	}
	tok := jwt.NewWithClaims(jwt.SigningMethodRS256, claims)
	signed, err := tok.SignedString(s.key)
	return signed, exp, err
}

// Validate parses and verifies a token, enforcing RS256 and lifetime.
func (s *Service) Validate(tokenStr string) (jwt.MapClaims, error) {
	claims := jwt.MapClaims{}
	_, err := jwt.ParseWithClaims(tokenStr, claims, func(t *jwt.Token) (interface{}, error) {
		if _, ok := t.Method.(*jwt.SigningMethodRSA); !ok {
			return nil, errors.New("unexpected signing method")
		}
		return &s.key.PublicKey, nil
	}, jwt.WithValidMethods([]string{"RS256"}))
	if err != nil {
		return nil, err
	}
	return claims, nil
}

func boolStr(b bool) string {
	if b {
		return "true"
	}
	return "false"
}
