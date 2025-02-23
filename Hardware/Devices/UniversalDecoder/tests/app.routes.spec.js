const request = require('supertest')
const app = require('../app.routes')

describe('DecoderValueSensor', () => {
    it('should decode basic data', async () => {
        const res = await sendRequest('DecoderValueSensor', new Uint8Array([2]), 1);
        expect(res.statusCode).toEqual(200);
        expect(res.body).toEqual({
            value: '02', //hex
        });
    });

    it('should handle missing parameters', async () => {
        const res = await request(app)
            .get(`/api/DecoderValueSensor`)
            .send();
        expect(res.statusCode).toEqual(400);
        expect(res.body).toEqual({
            error: 'Invalid inputs: \'payload\' is missing, \'fport\' Invalid value',
        });
    });

    it('should handle wrong payload', async () => {
        const res = await request(app)
            .get(`/api/DecoderValueSensor`)
            .query({
                payload: "not-base64",
                fport: 1,
            })
            .send();
        expect(res.statusCode).toEqual(400);
        expect(res.body).toEqual({
            error: 'Invalid inputs: \'payload\' must be a base64 encoded string',
        });
    });

    it('should handle wrong fPort', async () => {
        const res = await sendRequest('DecoderValueSensor', new Uint8Array([0]), "not-int");
        expect(res.statusCode).toEqual(400);
        expect(res.body).toEqual({
            error: 'Invalid inputs: \'fport\' Invalid value',
        });
    });
});

describe('invalid-decoder', () => {
    it('should return error', async () => {
        const res = await sendRequest('invalid-decoder', new Uint8Array([0]), 1);
        expect(res.statusCode).toEqual(500);
        expect(res.body).toEqual({
            error: "No codec found: invalid-decoder",
            rawPayload: "AA=="
        });
    });
});

describe('loravisionshield', () => {
    it('should decode led state on', async () => {
        const res = await sendRequest('loravisionshield', new Uint8Array([1]), 1);
        expect(res.statusCode).toEqual(200);
        expect(res.body).toEqual({
            value: {
                ledState: "on"
            },
        });
    });

    it('should decode led state off', async () => {
        const res = await sendRequest('loravisionshield', new Uint8Array([0]), 1);
        expect(res.statusCode).toEqual(200);
        expect(res.body).toEqual({
            value: {
                ledState: "off"
            },
        });
    });
});

describe('tpl110-0292', () => {
    it('should decode parking status occupied', async () => {
        const res = await sendRequest('tpl110-0292', new Uint8Array([1]), 1);
        expect(res.statusCode).toEqual(200);
        expect(res.body).toEqual({
            value: {
                type: "parking status",
                occupied: true
            },
        });
    });

    it('should decode parking status not occupied', async () => {
        const res = await sendRequest('tpl110-0292', new Uint8Array([0]), 1);
        expect(res.statusCode).toEqual(200);
        expect(res.body).toEqual({
            value: {
                type: "parking status",
                occupied: false
            },
        });
    });

    it('should decode device heartbeat', async () => {
        const res = await sendRequest('tpl110-0292', new Uint8Array([0b101, 0b01010111]), 2); 
        expect(res.statusCode).toEqual(200);
        expect(res.body).toEqual({
            value: {
                occupied: true,

                temperature: 87,
                type: "heartbeat"
            },
        });
    });
});

// The following test has been disabled because decoder script was changed
// A fix in copy-codecs.js is required to re-enable the import of this decoder #1833
// describe('lw001-bg', () => {
//     it('should decode all 1s', async () => {
//         const res = await sendRequest('lw001-bg', '1111111111111111', 1);
//         expect(res.statusCode).toEqual(200);
//         expect(res.body).toEqual({
//             value: {
//                 barometer: 65792.1,
//                 batterylevel: 0.01,
//                 devicestatus: "1",
//                 firmwareversion: 101,
//                 humidity: 25.61,
//                 macversion: 0,
//                 temperature: -19.39,
//                 type: "Device Information Packet"
//             },
//         });
//     });
// });

function sendRequest(decoderName, payload, fPort) { 
    return request(app)
        .get(`/api/${decoderName}`)
        .query({
            payload: Buffer.from(payload).toString('base64'),
            fport: fPort,
            devEui: "0000000000000000",
        })
        .send();
}
