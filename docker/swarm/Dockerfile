# MES Agent Swarm container: dashboard + dispatcher + agents, always on.
#
# It works on an ISOLATED clone of the repository kept in the `swarm_work`
# named volume (like CI), so it never touches your Windows working tree and
# never mixes Windows/Linux node_modules or obj/bin artifacts.
#
# Toolchain: .NET 10 SDK (build/test + Testcontainers), Node 20, PowerShell 7,
# git, GitHub CLI, opencode, Playwright (chromium) for e2e.
FROM mcr.microsoft.com/dotnet/sdk:10.0

ARG NODE_VERSION=20.19.0
ARG PWSH_VERSION=7.4.6
ARG GH_VERSION=2.63.2

ENV DEBIAN_FRONTEND=noninteractive \
    NPM_CONFIG_PREFIX=/usr/local \
    TEMP=/tmp \
    TMPDIR=/tmp \
    PLAYWRIGHT_BROWSERS_PATH=/root/.cache/ms-playwright

RUN apt-get update \
 && apt-get install -y --no-install-recommends \
      git curl ca-certificates xz-utils tar jq \
 && rm -rf /var/lib/apt/lists/*

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

# opencode + Playwright (browsers installed at build time)
RUN npm install -g opencode-ai@latest playwright@latest \
 && npx playwright install --with-deps chromium

COPY entrypoint.sh /usr/local/bin/swarm-entrypoint.sh
RUN chmod +x /usr/local/bin/swarm-entrypoint.sh

WORKDIR /work
EXPOSE 5178

ENTRYPOINT ["/usr/local/bin/swarm-entrypoint.sh"]
