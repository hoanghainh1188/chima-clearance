// Package hub hosts the SignalR hub that the UNCHANGED .NET MAUI client connects to.
// Server-side the .NET EventsHub is empty (push-only); we mirror that — no client→server
// methods, only server→client broadcasts (LedgerChanged, DailyHeaderOfficerChanged).
package hub

import (
	"log"

	"github.com/philippseith/signalr"
)

// AppHub is the empty, push-only hub mirroring B2.Server.Api/Realtime/EventsHub.cs.
type AppHub struct {
	signalr.Hub
}

func (h *AppHub) OnConnected(connectionID string) {
	log.Printf("[hub] client connected: %s", connectionID)
}

func (h *AppHub) OnDisconnected(connectionID string) {
	log.Printf("[hub] client disconnected: %s", connectionID)
}

// LedgerChanged payload — must serialize to {"declarationId":"<guid>"} to match
// LedgerChangedPayload ([JsonPropertyName("declarationId")]) in the desktop client.
type LedgerChanged struct {
	DeclarationID string `json:"declarationId"`
}

// DailyHeaderOfficerChanged payload — {"date":"yyyy-MM-dd","officerName":"..."}.
type DailyHeaderOfficerChanged struct {
	Date        string `json:"date"`
	OfficerName string `json:"officerName"`
}

// PushLedgerChanged broadcasts to all connected clients using the exact event name
// the desktop subscribes to via hub.On<LedgerChangedPayload>("LedgerChanged", ...).
func PushLedgerChanged(clients signalr.HubClients, declarationID string) {
	clients.All().Send("LedgerChanged", LedgerChanged{DeclarationID: declarationID})
	log.Printf("[hub] pushed LedgerChanged declarationId=%s", declarationID)
}
