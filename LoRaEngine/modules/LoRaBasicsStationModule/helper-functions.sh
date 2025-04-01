#!/bin/bash

DEFAULT_CERTS_PATH="/var/lorastarterkit/certs"
DEFAULT_TC_TRUST_PATH="$DEFAULT_CERTS_PATH/tc.trust"
DEFAULT_TC_CRT_PATH="$DEFAULT_CERTS_PATH/tc.crt"
DEFAULT_TC_KEY_PATH="$DEFAULT_CERTS_PATH/tc.key"
DEFAULT_CUPS_TRUST_PATH="$DEFAULT_CERTS_PATH/cups.trust"
DEFAULT_CUPS_CRT_PATH="$DEFAULT_CERTS_PATH/cups.crt"
DEFAULT_CUPS_KEY_PATH="$DEFAULT_CERTS_PATH/cups.key"
DEFAULT_SIG0_KEY_PATH="$DEFAULT_CERTS_PATH/sig-0.key"

conditionalCopy() {
    if [[ -z "$1" ]]; then
        echo "No proper path detected in environment variables. Trying to check for default location."
        if [ -z "$(ls -A $2 2> /dev/null)" ]; then
            echo "No file found at $2. Nothing was copied over."
        else
            echo "A file was found at $2. Copying it over."
            cp -v $2 .
        fi
    else
        cp -v $1 .
    fi
}

tcCertCopy() {
    if [[ "$TC_URI" == *"wss"* ]]; then
        echo "A secure protocol was specified for LNS endpoint. Copying over certificate files".
        conditionalCopy "$TC_TRUST_PATH" "$DEFAULT_TC_TRUST_PATH"
        conditionalCopy "$TC_CRT_PATH" "$DEFAULT_TC_CRT_PATH"
        conditionalCopy "$TC_KEY_PATH" "$DEFAULT_TC_KEY_PATH"
    fi
}

cupsCertCopy() {
    if [[ "$CUPS_URI" == *"https"* ]]; then
        echo "A CUPS endpoint was specified. Copying over certificate files".
        conditionalCopy "$CUPS_TRUST_PATH" "$DEFAULT_CUPS_TRUST_PATH"
        conditionalCopy "$CUPS_CRT_PATH" "$DEFAULT_CUPS_CRT_PATH"
        conditionalCopy "$CUPS_KEY_PATH" "$DEFAULT_CUPS_KEY_PATH"
        conditionalCopy "$SIG0_KEY_PATH" "$DEFAULT_SIG0_KEY_PATH"
    fi
}

RESET_PIN=23
POWER_PIN=18
GPIO_CHIP=gpiochip0
# SUPER-CONFUSINGLY, apt-get install gpio installs version 1.6.3, https://web.git.kernel.org/pub/scm/libs/libgpiod/libgpiod.git/?h=v1.6.x, whose
# interface is completely different than v2.X! 
GPIOSET="gpioset -m time -u 100000 ${GPIO_CHIP}"

resetPin() {
    echo "Resetting the pin"
    #./reset_lgw.sh stop $RESET_PIN
    #./reset_lgw.sh start $RESET_PIN
    # Prior version of reset_lgw.sh at https://github.com/lorabasics/basicstation/blob/master/examples/corecell/reset_lgw.sh.
    # upgraded to 6.6+ kernel's inteface. (https://github.com/lorabasics/basicstation/issues/206 and https://forums.raspberrypi.com/viewtopic.php?t=367431) in
    # NOTE: reset_lgw.sh never used $RESET_PIN parameter, so it was removed.

    echo "CoreCell power enable through GPIO$POWER_PIN..."
    ${GPIOSET} ${POWER_PIN}=1 2>/dev/null
    
    echo "CoreCell reset through GPIO$RESET_PIN..."
    ${GPIOSET} "${RESET_PIN}"=0 2>/dev/null
    ${GPIOSET} "${RESET_PIN}"=1 2>/dev/null
    ${GPIOSET} "${RESET_PIN}"=0 2>/dev/null
    echo "Finished resetting the pin"
}

setFixedStationEui() {
    if [[ -z "$FIXED_STATION_EUI" ]] || [[ $FIXED_STATION_EUI == '$LBS_FIXED_STATION_EUI' ]]; then
        echo "No custom station EUI is set, the basic station will select an EUI"
        sed -i 's/\"routerIdPlaceholder\": \"routerIdPlaceholder\",//g' station.conf
    else
        echo "Basic station will start with custom EUI: $FIXED_STATION_EUI"
        sed -i "s/\"routerIdPlaceholder\": \"routerIdPlaceholder\",/\"routerid\":\"$FIXED_STATION_EUI\",/g" station.conf
    fi
}

conditionallySetupCups() {
    if [[ -z "$CUPS_URI" ]]; then
        echo "Will start in NO_CUPS mode as no CUPS_URI has been specified."
    else
        cupsCertCopy
        echo "CUPS_URI is set to: $CUPS_URI"
        touch cups.uri && echo "$CUPS_URI" > cups.uri
    fi
}

conditionallySetupTc() {
    if [[ -z "$TC_URI" ]]; then
        echo "No TC_URI detected in environment variables."
    else
        tcCertCopy
        echo "TC_URI is set to: $TC_URI"
        touch tc.uri && echo "$TC_URI" > tc.uri
    fi
}

setLogLevel() {
    if [[ -z "$LOG_LEVEL" ]]; then
        echo "No custom LOG_LEVEL has been set. Defaulting to INFO."
    else
        sed -i "s/\"log_level\": \"INFO\",/\"log_level\":\"$LOG_LEVEL\",/g" station.conf
    fi
}
