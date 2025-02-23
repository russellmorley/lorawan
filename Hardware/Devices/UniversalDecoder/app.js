'use strict';

let port = 80;
    
const app = require('./app.routes');
if (process.argv.length > 2) {
    port = process.argv[2];
}
app.listen(port, () => {
  console.log(`Server started at http://localhost:${port}`)
})
