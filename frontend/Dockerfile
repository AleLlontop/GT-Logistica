FROM node:20-alpine
WORKDIR /app

COPY frontend/package.json frontend/package-lock.json ./
RUN npm ci

COPY frontend/ ./
EXPOSE 5173

# En desarrollo el servidor de Vite hace de proxy hacia el backend (ver vite.config.ts),
# de modo que el navegador vea backend y frontend en el mismo origen.
CMD ["npm", "run", "dev", "--", "--host", "0.0.0.0"]
