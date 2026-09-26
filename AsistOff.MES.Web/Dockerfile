# Build stage
# Pinned by digest (issue #271).
FROM node:26-alpine@sha256:0b36e8c136b94cd4fcf02188228e76c31ad5872eef3fec8cbd2eee500cfd9e80 AS build
WORKDIR /app

COPY package.json package-lock.json ./
RUN npm ci

COPY . .

# VITE_API_BASE_URL is only the build-time fallback for the API base URL
# reachable from the user's browser. At container start docker-entrypoint.sh
# renders the runtime value (API_BASE_URL env) to /config.js, which the app
# prefers — so production never needs a rebuild to repoint the backend.
ARG VITE_API_BASE_URL=http://localhost:8080
ENV VITE_API_BASE_URL=$VITE_API_BASE_URL

RUN npm run build

# Serve stage
# Pinned by digest (issue #271). Runs as the non-root `nginx` user on
# unprivileged port 8080: `docker exec <web> id -u` reports a non-zero UID.
FROM nginx:alpine@sha256:1ed1b0e1d7652937d6cbdaf4018c7b6fc009a7dd6c3047351e2eddda745de43f AS runtime
COPY --from=build /app/dist /usr/share/nginx/html
COPY nginx.conf /etc/nginx/conf.d/default.conf
COPY docker-entrypoint.sh /docker-entrypoint.sh

RUN chmod +x /docker-entrypoint.sh \
    && chown -R nginx:nginx /usr/share/nginx/html /var/cache/nginx /var/log/nginx /etc/nginx/conf.d \
    && touch /run/nginx.pid && chown nginx:nginx /run/nginx.pid

EXPOSE 8080

USER nginx

ENTRYPOINT ["/docker-entrypoint.sh"]
