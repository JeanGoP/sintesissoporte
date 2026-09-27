FROM node:24-alpine AS build
WORKDIR /app
COPY src/sidecil-tickets-web/package*.json ./
RUN npm ci
COPY src/sidecil-tickets-web/ ./
ARG VITE_API_URL
ENV VITE_API_URL=$VITE_API_URL
RUN node -e "const u=new URL(process.env.VITE_API_URL); if(u.protocol!=='https:' || u.search || u.hash || u.username || u.password) throw Error('VITE_API_URL debe ser la URL HTTPS del backend');" && npm run build:frontend

FROM nginx:stable-alpine
COPY deploy/coolify/nginx.conf /etc/nginx/conf.d/default.conf
COPY --from=build /app/dist /usr/share/nginx/html
EXPOSE 80
HEALTHCHECK --interval=30s --timeout=3s CMD wget -q -O /dev/null http://127.0.0.1/health || exit 1
