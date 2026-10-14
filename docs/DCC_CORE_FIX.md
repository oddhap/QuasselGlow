# DCC CHAT forwarding for Quassel cores

Quassel 0.14.0 replaces incoming DCC requests with a server message such as
`Received unknown CTCP-DCC request by openmud!user@host`. That message contains
neither the offered address nor its port. QuasselGlow cannot reconstruct them.
The same limitation affects QuasselMud.

The patch in `patches/quassel-core-dcc-chat.patch` changes the core's event
stringifier to forward incoming DCC CHAT requests as plain messages, preserving
the original parameters, sender and target. QuasselGlow already understands
these messages and offers **Accept DCC chat**. Other DCC requests and CTCP
responses retain their existing handling. The core does not accept invitations
or establish direct connections.

## Apply to a core source checkout

From a Quassel 0.14.0 source checkout:

```sh
git apply --check /path/to/QuasselGlow/patches/quassel-core-dcc-chat.patch
git apply /path/to/QuasselGlow/patches/quassel-core-dcc-chat.patch
cmake -S . -B build -G Ninja -DWANT_CORE=ON -DWANT_QTCLIENT=OFF -DWANT_MONO=OFF -DCMAKE_BUILD_TYPE=Release
cmake --build build --target quasselcore --parallel 2
```

With a C++ compiler and Qt5Core development files available through `pkg-config`,
test the patched handler before installing:

```sh
python3 /path/to/QuasselGlow/scripts/test-core-dcc-forwarding.py src/core/eventstringifier.cpp
```

This compiles the actual patched `defaultHandler` against a small event fixture
and checks CHAT forwarding, sender/target preservation, and the unchanged
behavior of file offers, other CTCP commands, replies and self events.

Install the built core using your host's service configuration, keeping the
existing data directory, user, TLS options and authentication features. Back up
the database while the service is stopped before switching the executable.
If using the default shared-library build, install the accompanying
`build/lib/libquassel-*.so*` files too and configure the service's library path.
Restarting the core briefly disconnects IRC and desktop sessions.

Request a **new** invitation with `!play` in the OpenMUD query, then choose
**Accept DCC chat**. Old diagnostic messages and expired invitations cannot be
used. QuasselGlow still rejects malformed offers, unsupported protocols and
invalid endpoints, requires explicit acceptance, and disables backlog offers.

The reference implementation is
[Quassel's event stringifier](https://github.com/quassel/quassel/blob/0.14.0/src/core/eventstringifier.cpp).
Future core updates may require reapplying and rebuilding the patch.
