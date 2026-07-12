#!/usr/bin/env bash

set -euo pipefail

repo_root="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
project_path="$repo_root/src/QuasselGlow/QuasselGlow.csproj"
props_path="$repo_root/Directory.Build.props"

version="$(sed -n 's:.*<VersionPrefix>\(.*\)</VersionPrefix>.*:\1:p' "$props_path" | head -n 1)"
if [[ -z "$version" ]]; then
  echo "Could not determine VersionPrefix from $props_path" >&2
  exit 1
fi

machine_arch="$(uname -m)"
case "$machine_arch" in
  x86_64) runtime_identifier="linux-x64" ;;
  aarch64|arm64) runtime_identifier="linux-arm64" ;;
  *)
    echo "Unsupported Linux architecture: $machine_arch" >&2
    exit 1
    ;;
esac

release_root="$repo_root/.artifacts/releases/v$version/$runtime_identifier"
binary_source="$release_root/QuasselGlow"

install_root="${XDG_DATA_HOME:-$HOME/.local/share}"
app_root="$HOME/.local/opt/QuasselGlow"
desktop_target_dir="$install_root/applications"
icon_target_dir="${XDG_DATA_HOME:-$HOME/.local/share}/icons/hicolor/128x128/apps"
desktop_target="$desktop_target_dir/quasselglow.desktop"
icon_target="$icon_target_dir/quasselglow.png"
binary_target="$app_root/QuasselGlow"

export PATH="${DOTNET_ROOT:-$HOME/.dotnet}:$PATH"
export DOTNET_CLI_HOME="${DOTNET_CLI_HOME:-$repo_root/.dotnet-home}"
export NUGET_PACKAGES="${NUGET_PACKAGES:-$repo_root/.nuget/packages}"
export DOTNET_SKIP_FIRST_TIME_EXPERIENCE="${DOTNET_SKIP_FIRST_TIME_EXPERIENCE:-1}"
export DOTNET_ADD_GLOBAL_TOOLS_TO_PATH="${DOTNET_ADD_GLOBAL_TOOLS_TO_PATH:-false}"
export DOTNET_CLI_TELEMETRY_OPTOUT="${DOTNET_CLI_TELEMETRY_OPTOUT:-1}"

mkdir -p "$DOTNET_CLI_HOME" "$NUGET_PACKAGES"

dotnet publish "$project_path" \
  -c Release \
  -r "$runtime_identifier" \
  --self-contained true \
  --force \
  -p:PublishSingleFile=true \
  -p:IncludeNativeLibrariesForSelfExtract=true \
  -p:DebugType=None \
  -p:DebugSymbols=false \
  -maxcpucount:1 \
  -nodeReuse:false \
  --output "$release_root"

mkdir -p "$app_root" "$desktop_target_dir" "$icon_target_dir"

install -m 755 "$binary_source" "$binary_target"
install -m 644 "$repo_root/src/QuasselGlow/Assets/Icons/icon_128.png" "$icon_target"

cat > "$desktop_target" <<EOF
[Desktop Entry]
Version=1.0
Type=Application
Name=QuasselGlow
Comment=Cross-platform desktop client for the Quassel protocol
Exec=$binary_target
Icon=$icon_target
Terminal=false
Categories=Network;Chat;InstantMessaging;
StartupNotify=true
EOF

if command -v update-desktop-database >/dev/null 2>&1; then
  update-desktop-database "$desktop_target_dir" || true
fi

echo "Installed QuasselGlow $version"
echo "Binary: $binary_target"
echo "Launcher: $desktop_target"
