Provides a HTTP REST GET endpoint for decoding LoRaWan packets received from LoRaWan end devices. Uses the decoders in [The Things Network repository](https://github.com/TheThingsNetwork/lorawan-devices#payload-codecs).

## Build

### Decoders
```
npm install
npm run codecs
```

## Docker Images

### Multi-platform

`docker build . --platform linux/amd64,linux/arm64 -f Dockerfile -t [ORG]/universaldecoder:[VERSION]`

### ARM64

`docker build . --platform=linux/arm64 -f Dockerfile.arm64v8 -t [ORG]/universaldecoder:[VERSION]-arm64`

### AMD64

`docker build . -f Dockerfile.amd64 -t [ORG]/universaldecoder:[VERSION]-amd64`

## Usage

### Start the server: 
`docker run --rm -d -p 8800:80 [ORG]/universaldecoder:[VERSION]`

### Decode:
#### Endpoint uses the following URL pattern:
`/api/<decoder>?devEui=<devEui>&fport=<fport>&payload=<payload>`

Where:
- decoder: identifies the TTN decoder that will be used. You can get a list of all available decoders by calling the /decoders endpoint.
- devEui: LoRaWan unique end-device identifier.
- fport:  LoRaWan Port field as integer value.
- payload: Base64 and URL encoded byte[] payload (Uint8Array) to decode. For example, to send the number 2 in binary, payload is `Uint8Array([2])`
    - Convert it to a base64 encoded string: Ag==
    - Convert the result to a valid URL parameter: Ag%3D%3D
    - Add this to your URL as the payload query parameter.

### Example 1:
`curl 'http://localhost:8080/api/DecoderValueSensor?devEui=0000000000000000&fport=1&payload=Ag%3D%3D'`

Decodes to: 
`{"value":"02"}`

### Example 2:

Based on [Milesight controller, page 4](https://resource.milesight.com/milesight/iot/document/uc11-series-communication-protocol-en.pdf):

Hex: 01 00 01 02 c8 06 00 00 00 09 01 00 0a 01 01
Base64: AQABAsgGAAAACQEACgEB   [hex to base64 tool](https://base64.guru/converter/encode/hex)

`curl 'http://localhost:8080/api/uc1114?deveui=0000000000000000&fport=1&payload=AQABAsgGAAAACQEACgEB'`

Decodes to: 
`{"value":{"digital_input_1":"on","digital_output_1":"off","digital_output_2":"on"}}`

### List all decoders:
`curl 'http://localhost:8080/decoders'`

## Local Development

`npm run debug`

## Tests

`npm test`

## Integration

### IOT EDGE as a Module

Edit module.json to point to the container registry containing the image.

