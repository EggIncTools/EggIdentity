#!/usr/bin/env bash
# Idempotent install/update for EggIdentity.Agent on a Linux Docker host. Run as root (or via sudo).
# Publishes one shared binary, then enables a per-instance systemd unit via eggidentity-agent@<name>.
# Usage: install.sh <instance-name>   e.g. install.sh eggidentity
set -euo pipefail

INSTANCE="${1:?usage: install.sh <instance-name>}"
REPO_URL="${EGGIDENTITY_REPO_URL:-https://github.com/EggIncTools/eggidentity.git}"
INSTALL_DIR="${EGGIDENTITY_INSTALL_DIR:-/opt/eggidentity-agent}"
SERVICE_USER="${EGGIDENTITY_SERVICE_USER:-eggidentity-agent}"
SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"

if [ "$(id -u)" -ne 0 ]; then
  echo "install.sh: must run as root" >&2
  exit 1
fi

if ! id "$SERVICE_USER" >/dev/null 2>&1; then
  useradd --system --create-home --home-dir "/home/$SERVICE_USER" --shell /usr/sbin/nologin "$SERVICE_USER"
fi

# The agent shells out to `docker` on the host, so its user needs docker-group membership.
if getent group docker >/dev/null 2>&1; then
  usermod -aG docker "$SERVICE_USER"
fi

mkdir -p /etc/eggidentity
ENV_FILE="/etc/eggidentity/$INSTANCE.env"
if [ ! -f "$ENV_FILE" ]; then
  cat > "$ENV_FILE" <<EOF
# EggIdentity.Agent runtime config for instance "$INSTANCE". See EggIdentity.Agent/Program.cs for the full env var list.
DEPLOY_AGENT_SECRET=changeme
DEPLOY_AGENT_PORT=7777
DEPLOY_AGENT_CONFIG=/etc/eggidentity/$INSTANCE.yaml
EOF
  chmod 600 "$ENV_FILE"
  chown "$SERVICE_USER:$SERVICE_USER" "$ENV_FILE"
  echo "install.sh: wrote $ENV_FILE with a placeholder secret and default port 7777, edit before starting (pick a free port if other instances are already running)"
fi

git config --global --add safe.directory "$INSTALL_DIR"

if [ -d "$INSTALL_DIR/.git" ]; then
  git -C "$INSTALL_DIR" pull
else
  git clone "$REPO_URL" "$INSTALL_DIR"
fi

dotnet publish "$INSTALL_DIR/EggIdentity.Agent" -c Release -o "$INSTALL_DIR/publish"
chown -R "$SERVICE_USER:$SERVICE_USER" "$INSTALL_DIR"

install -m 644 "$SCRIPT_DIR/eggidentity-agent@.service" "/etc/systemd/system/eggidentity-agent@.service"
systemctl daemon-reload
systemctl enable "eggidentity-agent@$INSTANCE"
systemctl restart "eggidentity-agent@$INSTANCE"

echo
echo "eggidentity-agent@$INSTANCE installed and running."
echo "Next steps:"
echo "  1. Edit $ENV_FILE (DEPLOY_AGENT_SECRET, DEPLOY_AGENT_PORT) if you haven't already."
echo "  2. Write /etc/eggidentity/$INSTANCE.yaml with your pipeline (see EggIdentity.Agent/Program.cs header)."
echo "  3. systemctl restart eggidentity-agent@$INSTANCE"
echo "  4. journalctl -u eggidentity-agent@$INSTANCE -f"
