# syntax=docker/dockerfile:1.7
# Student portal — Nx/Angular SPA built then served by nginx (same-origin: nginx proxies /api + /hubs to the
# internal API, keeping the sb_device cookie first-party). Build context = frontend/ (the Nx root).
# See docs/IMPLEMENTATION-PLAN-deployment.md §5.
ARG APP=student-portal
ARG NX_CONFIG=production

# ---- build ----
FROM node:24-alpine AS build
WORKDIR /app
ARG APP
ARG NX_CONFIG
COPY package.json package-lock.json .npmrc nx.json tsconfig.base.json ./
RUN --mount=type=cache,target=/root/.npm npm ci
COPY . .
RUN npx nx build "$APP" --configuration="$NX_CONFIG" \
 && test -f "dist/apps/$APP/browser/index.html"

# ---- serve ----
FROM nginx:1.27-alpine AS runtime
ARG APP
RUN rm -rf /usr/share/nginx/html/*
COPY apps/${APP}/nginx.conf /etc/nginx/conf.d/default.conf
COPY --from=build /app/dist/apps/${APP}/browser /usr/share/nginx/html
EXPOSE 80
CMD ["nginx", "-g", "daemon off;"]
