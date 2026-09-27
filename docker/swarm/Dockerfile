# MES Agent Swarm container: dashboard + dispatcher + agents, always on.
#
# It works on an ISOLATED clone of the repository kept in the `swarm_work`
# named volume (like CI), so it never touches your Windows working tree and
# never mixes Windows/Linux node_modules or obj/bin artifacts.
#
# Toolchain: .NET 10 SDK (build/test + Testcontainers), Node 20, PowerShell 7,
# git, GitHub CLI, opencode, Playwright (chromium) for e2e.
#
# Security hardening (issue #357):
# - runs as non-root `swarm` (UID 10000): `docker exec <swarm> id -u`
#   reports a non-zero UID;
# - Docker socket access is least-privilege via docker-group GID membership
#   (build arg DOCKER_GID must match the host `docker` group GID), never
#   via root or privileged mode;
# - host AI-service credentials are never bind-mounted: opencode
#   authenticates via the env-passed OPENCODE_API_KEY (same model as
#   GH_TOKEN) and container-local state lives in a scoped named volume
#   (see docker-compose.swarm.yml).
#
# Pinned by digest (issue #271 model, shared with the app Dockerfile): refresh with
#   docker build --provenance=false ./docker/swarm && docker inspect <image> --format '{{.RepoDigests}}'
FROM mcr.microsoft.com/dotnet/sdk:10.0@sha256:35d40304542c8689331f8cab17c65926cdf48fe711e289321d71924b230a7d29

ARG NODE_VERSION=20.19.0
ARG PWSH_VERSION=7.4.6
ARG GH_VERSION=2.63.2
# GID of the host `docker` group (least-privilege socket access).
# Linux: `getent group docker` (usually 999). Docker Desktop keeps the
# socket group-readable inside the Linux VM, so the default works there too;
# override via DOCKER_GID in .env when the host uses another GID.
ARG DOCKER_GID=999

ENV DEBIAN_FRONTEND=noninteractive \
    NPM_CONFIG_PREFIX=/usr/local \
    TEMP=/tmp \
    TMPDIR=/tmp \
    HOME=/home/swarm \
    XDG_CONFIG_HOME=/home/swarm/.config \
    XDG_DATA_HOME=/home/swarm/.local/share \
    npm_config_cache=/home/swarm/.npm \
    PLAYWRIGHT_BROWSERS_PATH=/home/swarm/.cache/ms-playwright

RUN apt-get update \
 && apt-get install -y --no-install-recommends \
      git curl ca-certificates xz-utils tar jq \
 && rm -rf /var/lib/apt/lists/*

# Non-root runtime user + docker-group membership for the bind-mounted
# socket. The socket file is root:<docker-gid> on the host, so membership
# in a container group with the SAME GID grants Testcontainers access
# without root. `getent` keeps the build working when the base image
# already owns the GID under another name.
RUN if ! getent group "${DOCKER_GID}" >/dev/null 2>&1; then \
      groupadd -r -g "${DOCKER_GID}" docker; \
    fi \
 && DOCKER_GROUP="$(getent group "${DOCKER_GID}" | cut -d: -f1)" \
 && useradd -m -u 10000 -s /bin/bash swarm \
 && usermod -aG "${DOCKER_GROUP}" swarm \
 && mkdir -p /work /home/swarm/.cache/ms-playwright \
      /home/swarm/.config /home/swarm/.local/share/opencode \
      /home/swarm/.npm \
 && chown -R swarm:swarm /work /home/swarm

# Node.js 20 (official tarball — distro-agnostic)
RUN curl -fsSL "https://nodejs.org/dist/v${NODE_VERSION}/node-v${NODE_VERSION}-linux-x64.tar.xz" -o /tmp/node.tar.xz \
 && mkdir -p /usr/local/lib/nodejs \
 && tar -xJf /tmp/node.tar.xz -C /usr/local/lib/nodejs \
 && ln -sf "/usr/local/lib/nodejs/node-v${NODE_VERSION}-linux-x64/bin/node" /usr/local/bin/node \
 && ln -sf "/usr/local/lib/nodejs/node-v${NODE_VERSION}-linux-x64/bin/npm" /usr/local/bin/npm \
 && ln -sf "/usr/local/lib/nodejs/node-v${NODE_VERSION}-linux-x64/bin/npx" /usr/local/bin/npx \
 && rm /tmp/node.tar.xz

# GitHub CLI (official tarball)
RUN curl -fsSL "https://github.com/cli/cli/releases/download/v${GH_VERSION}/gh_${GH_VERSION}_linux_amd64.tar.gz" -o /tmp/gh.tar.gz \
 && tar -xzf /tmp/gh.tar.gz -C /tmp \
 && mv "/tmp/gh_${GH_VERSION}_linux_amd64/bin/gh" /usr/local/bin/gh \
 && rm -rf /tmp/gh.tar.gz "/tmp/gh_${GH_VERSION}_linux_amd64"

# PowerShell 7 (official tarball)
RUN curl -fsSL "https://github.com/PowerShell/PowerShell/releases/download/v${PWSH_VERSION}/powershell-${PWSH_VERSION}-linux-x64.tar.gz" -o /tmp/pwsh.tar.gz \
 && mkdir -p /opt/microsoft/powershell/7 \
 && tar -xzf /tmp/pwsh.tar.gz -C /opt/microsoft/powershell/7 \
 && chmod +x /opt/microsoft/powershell/7/pwsh \
 && ln -sf /opt/microsoft/powershell/7/pwsh /usr/local/bin/pwsh \
 && rm /tmp/pwsh.tar.gz

# opencode + Playwright (browsers installed at build time).
# System deps need root (--with-deps installs apt packages), then the
# browser cache is handed to `swarm` so the runtime user can run e2e.
RUN npm install -g opencode-ai@latest playwright@latest \
 && npx playwright install --with-deps chromium \
 && chown -R swarm:swarm /home/swarm

COPY entrypoint.sh /usr/local/bin/swarm-entrypoint.sh
RUN chmod +x /usr/local/bin/swarm-entrypoint.sh

WORKDIR /work
EXPOSE 5178

# Non-root by default (issue #357). The compose file pins the same user
# explicitly so `docker compose config` never shows a root user.
USER swarm

ENTRYPOINT ["/usr/local/bin/swarm-entrypoint.sh"]
