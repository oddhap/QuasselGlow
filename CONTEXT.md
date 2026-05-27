# QuasselGlow

QuasselGlow is a desktop client for people who use a Quassel core as their IRC home base. This context defines the product language around dependable everyday chat use.

## Language

**Daily-driver reliability**:
Confidence that QuasselGlow can stay connected, preserve local user intent, recover understandably from failures, and remain safe enough for everyday IRC use against a real Quassel core.
_Avoid_: polish, stability, reliability

**Quassel core**:
The remote Quassel server process that owns IRC network connections, persistent buffers, backlog, authentication, and synchronized chat state.
_Avoid_: server, backend

**Desktop client**:
The local QuasselGlow application that presents Quassel core state, accepts user input, and stores local preferences or cache data.
_Avoid_: frontend, UI

**Local user state**:
User-owned state kept by the desktop client rather than the Quassel core, including connection preferences, appearance choices, composer drafts, input history, unread markers, and message cache.
_Avoid_: settings, cache, app state

**Connection preferences**:
The local user state that tells the desktop client how to reconnect to a Quassel core, including host, port, username, TLS trust choice, remember-login choice, auto-connect choice, language, appearance, layout, and tray behavior.
_Avoid_: settings, config

**Credential protection**:
The desktop client's ability to store remembered login secrets in a way that is protected by the local operating system or clearly marked as degraded when that is not available.
_Avoid_: password storage, encryption

**Degraded credential protection**:
Credential protection that still allows remembered login, but stores the remembered secret without operating-system protection and must therefore remain visible as degraded local user state.
_Avoid_: insecure mode, fallback encryption

**Message cache**:
A local copy of recent buffer messages used by the desktop client to make channel startup and reconnect feel continuous before fresh backlog arrives from the Quassel core.
_Avoid_: backlog, history

**Degraded message cache**:
Message cache that cannot currently be read from or written to reliably by the desktop client. It is a global desktop client condition, not a per-buffer user-facing state.
_Avoid_: buffer cache error, backlog failure

**Visible failure**:
A failure that the desktop client surfaces in a way the user can understand and act on, especially when local user state could not be loaded, saved, protected, or refreshed.
_Avoid_: error, exception, warning

**Discreet visible failure**:
A visible failure that does not interrupt chat use, but remains discoverable in the desktop client until the user understands what local user state is degraded.
_Avoid_: modal error, blocking alert, silent failure

**Status area**:
The existing desktop client surface that communicates connection state and short operational details without interrupting chat use.
_Avoid_: toast, modal, notification

**Highest-priority visible failure**:
The single active visible failure from the most important degraded local user state boundary, ordered as connection preferences, credential protection, then message cache.
_Avoid_: error list, warning queue

**Recovered local user state**:
A previously degraded local user state boundary that has been proven healthy by a later successful load, save, protection, or cache operation.
_Avoid_: dismissed error, acknowledged warning

**Composer addressing**:
Starting a channel message by naming a channel user directly so the message is visibly addressed to that nick.
_Avoid_: first-word autocomplete, mention prefix

**Inline nick completion**:
Completing the message token that the caret is inside to a channel user's nick inside an already-started message without turning the message into direct address.
_Avoid_: mid-message addressing, inline mention

**Wallpaper-matched appearance**:
An appearance choice where the desktop client reflects the user's desktop background colors while preserving readable chat surfaces and falling back gracefully when those colors are unavailable.
_Avoid_: dynamic theme, wallpaper sync, system accent theme

## Example Dialogue

Dev: Should the next release focus on more Quassel features or daily-driver reliability?

Domain expert: Daily-driver reliability. The desktop client should reconnect clearly, keep drafts and local preferences intact, avoid silent data-loss-feeling failures, and make it obvious when the Quassel core or local machine state is the source of a problem.

Dev: Which part of daily-driver reliability matters most first?

Domain expert: Local user state and visible failures. Users should trust their local preferences and chat workflow state, and the desktop client should not quietly ignore problems that affect that trust.

Dev: Should local user state failures block chat use?

Domain expert: No. They should be discreet but visible. The desktop client should keep chat usable while making degraded local state understandable.

Dev: Which local user state failures should be handled first?

Domain expert: Connection preferences first, credential protection second, and message cache third. Those are the first daily-driver trust boundaries to make visible.

Dev: Is plain remembered-login storage acceptable on platforms without operating-system protection?

Domain expert: It may remain functional, but it is degraded credential protection and must become visible after the desktop client saves remembered login without operating-system protection.

Dev: Where should discreet visible failures appear first?

Domain expert: In the status area. It already carries operational state, so local user state degradation can become visible without adding a new notification system.

Dev: Should the status area show every local user state failure?

Domain expert: No. Show the highest-priority visible failure first: connection preferences, then credential protection, then message cache.

Dev: When should a visible failure disappear?

Domain expert: It should disappear automatically once the same local user state boundary is recovered by a successful operation.

Dev: Does credential protection remain degraded after remembered login is turned off?

Domain expert: No. A successful save without a remembered secret recovers credential protection.

Dev: Should message cache degradation be shown per buffer?

Domain expert: No. Treat degraded message cache as a global desktop client condition.

Dev: When is degraded message cache recovered?

Domain expert: After the next successful message cache read or write.

Dev: Should nick completion always produce an addressed message?

Domain expert: No. Composer addressing happens only at the start of the message. Inline nick completion should insert the nick without making the whole message look addressed to that user.

Dev: Should inline nick completion clean up the surrounding message text?

Domain expert: No. It should replace only the current token and preserve the surrounding text exactly.

Dev: Should wallpaper-matched appearance depend on the desktop background being readable by the app?

Domain expert: No. It should use desktop background colors when available, but fall back gracefully so chat remains readable.

Dev: Should wallpaper-matched appearance only exist on platforms where wallpaper colors can be read?

Domain expert: No. It should remain a cross-platform appearance choice, with platform-specific color discovery improving it where available.

Dev: Should wallpaper-matched appearance let desktop background colors control the whole chat surface?

Domain expert: No. It should use desktop background colors for mood, accents, and backdrop treatment while keeping chat text and message surfaces conservatively readable.

Dev: Does wallpaper-matched appearance decide whether the desktop client is light or dark?

Domain expert: No. The user's light or dark appearance choice remains separate, and wallpaper colors adapt to that choice.

Dev: When multiple desktop backgrounds are available, which one should wallpaper-matched appearance reflect?

Domain expert: The main screen or current desktop background should drive the appearance first. Combining backgrounds or following the window between screens can wait until there is a clear user need.
