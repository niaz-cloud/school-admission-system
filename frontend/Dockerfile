# ---- build ------------------------------------------------------------------------------
FROM node:20-alpine AS build
WORKDIR /app
COPY package.json package-lock.json ./
RUN npm ci --no-audit --no-fund
COPY . .
RUN npm run build

# ---- test (used by `docker compose --profile test run --rm frontend-tests`) ---------------
FROM build AS test
CMD ["npx", "vitest", "run"]

# ---- runtime ----------------------------------------------------------------------------
FROM nginx:1.27-alpine AS final
COPY nginx.conf /etc/nginx/conf.d/default.conf
COPY docker-entrypoint.d/40-runtime-config.sh /docker-entrypoint.d/40-runtime-config.sh
RUN chmod +x /docker-entrypoint.d/40-runtime-config.sh
COPY --from=build /app/dist /usr/share/nginx/html
ENV API_BASE_URL=http://localhost:8080
EXPOSE 80
