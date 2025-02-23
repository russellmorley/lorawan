
FROM node:23-slim

WORKDIR /app/

COPY package*.json ./
COPY NOTICE.txt ./

RUN npm install --production

COPY *.js ./
COPY codecs ./codecs

USER node

CMD ["node", "app.js"]
