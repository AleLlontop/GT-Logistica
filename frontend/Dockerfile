FROM node:24-alpine
WORKDIR /app

# El `.npmrc` va en esta capa y no con el resto: trae `engine-strict`, y si llegara
# después el `npm ci` de acá abajo ya habría corrido sin verificar la versión de Node.
COPY frontend/package.json frontend/package-lock.json frontend/.npmrc ./
RUN npm ci

COPY frontend/ ./
EXPOSE 5173

# En desarrollo el servidor de Vite hace de proxy hacia el backend (ver vite.config.ts),
# de modo que el navegador vea backend y frontend en el mismo origen.
CMD ["npm", "run", "dev", "--", "--host", "0.0.0.0"]
