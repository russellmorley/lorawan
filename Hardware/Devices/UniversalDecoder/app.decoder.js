'use strict';

const {logger} = require('./app.logging');

const decoders = (() => {
    try {
        return require('./codecs');
    } catch (e) {
        if (e instanceof Error && e.code === 'MODULE_NOT_FOUND') {
            return {};
        } else {
            throw e;
        }
    }
})();

function getAllDecoders() {
    return Object.keys(decoders);
}

// gets decoder by name
function getDecoder(decoderName) {
    if (decoderName === 'DecoderValueSensor') {
        // return inline decoder implementation
        return {
            decodeUplink: (input) => { return { data: input.bytes.toString('hex') } }
        }
    }
    const decoder = decoders[decoderName];
    if (!decoder) {
        throw new Error(`No codec found: ${decoderName}`);
    }
    return decoder;
}

function decode(decoderName, payload, fPort) {
    const decoder = getDecoder(decoderName);

    const input = {
        //was:
        //bytes: Buffer.from(payload, 'base64').toString('utf8').split(''),

        //the next two appear to be equivalent, and 
        bytes: Buffer.from(payload, 'base64'),
        //bytes: Uint8Array.from(atob(payload), c => c.charCodeAt(0)),

        fPort: parseInt(fPort)
    };

    logger.debug(`Decoder ${decoderName} input: ${JSON.stringify(input)}`);
    const output = decoder.decodeUplink(input);
    logger.debug(`Decoder ${decoderName} output: ${JSON.stringify(output)}`);

    return output;
}

module.exports = { getAllDecoders, decode };
