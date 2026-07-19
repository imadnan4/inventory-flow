FROM oven/bun:1.3.14 AS build
WORKDIR /app

COPY package.json bun.lock ./
RUN bun install --frozen-lockfile
COPY . .
ARG VITE_API_BASE_URL=/
ENV VITE_API_BASE_URL=$VITE_API_BASE_URL
RUN bun run build

FROM nginxinc/nginx-unprivileged:1.27-alpine AS final
COPY --chown=nginx:nginx nginx.conf /etc/nginx/conf.d/default.conf
COPY --chown=nginx:nginx --from=build /app/dist /usr/share/nginx/html
EXPOSE 8080
