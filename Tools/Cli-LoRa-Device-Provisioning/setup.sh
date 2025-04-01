# Creates the devices in azure IOT hub.
# From https://azure.github.io/iotedge-lorawan-starterkit/dev/quickstart/#deployed-azure-infrastructure "deploy to Azure"
# I believe this script 
#   1. sets up the devices in Azure IOT hub
#   2. puts the configuration in the device twin
#   When the device containers run, they obtain the twin and configure themselves.


create_devices_with_lora_cli() {
	echo \"Downloading lora-cli from $LORA_CLI_URL...\"
	curl -SsL \"$LORA_CLI_URL\" -o lora-cli.tar.gz
	mkdir -p lora-cli && tar -xzf ./lora-cli.tar.gz -C ./lora-cli
		
	cd lora-cli
	chmod +x ./loradeviceprovisioning
			
	local monitoringEnabled=\"false\"
	if [ \"${MONITORING_ENABLED}\" = \"1\" ]; then
		monitoringEnabled=\"true\"
	fi

	# Add --tenant-id to add a tenant id and  --tenant-key to add a signature to validate tenant id.
	# Add --local-redis-connection-string to add the connection string to the redis-compatible database, e.g. 'ghcr.io/microsoft/garnet'
	# Add --local-redis-module-image to add the url to a redis-compatible database, e.g. 'ghcr.io/microsoft/garnet'. This entry is required to include a local redis module.
	# Add --local-redis-params-escaped-jsonstring to add additional params to the container, e.g. "\\\"Ulimits\\\":[{\\\"memlock\\\":-1}]" to add a Ulimits container setting of memlock: -1
	#
	echo \"Creating gateway $EDGE_GATEWAY_NAME...\"
	./loradeviceprovisioning add-gateway --reset-pin \"$RESET_PIN\" --device-id \"$EDGE_GATEWAY_NAME\" --spi-dev \"$SPI_DEV\" --spi-speed \"$SPI_SPEED\" --devicemanagerservices-url \"$FACADE_SERVER_URL\" --devicemanagerservices-code \"$FACADE_AUTH_CODE\" --lns-host-address \"$LNS_HOST_ADDRESS\" --network \"$NETWORK\" --monitoring \"$monitoringEnabled\" --iothub-resource-id \"$IOTHUB_RESOURCE_ID\" --log-analytics-workspace-id \"$LOG_ANALYTICS_WORKSPACE_ID\" --log-analytics-shared-key \"$LOG_ANALYTICS_SHARED_KEY\" --lora-version \"$LORA_VERSION\"
			
	# Add --tenant-id to add a tenant id.
	echo \"Creating concentrator $STATION_DEVICE_NAME for region $REGION...\"
	./loradeviceprovisioning add --type concentrator --region \"$REGION\" --stationeui \"$STATION_DEVICE_NAME\" --no-cups --network \"$NETWORK\"
			
	# add leaf devices
	if [ \"${DEPLOY_DEVICE}\" = \"1\" ]; then
		echo \"Creating leaf devices 46AAC86800430028 and 47AAC86800430028...\"
		abp_apps_key=$(tr -dc 'A-F0-9' < /dev/urandom | head -c32)
		abp_nwks_key=$(tr -dc 'A-F0-9' < /dev/urandom | head -c32)
		otaa_key=$(tr -dc 'A-F0-9' < /dev/urandom | head -c32)
		# Add --tenant-id to add a tenant id.
		./loradeviceprovisioning add --type abp --deveui \"46AAC86800430028\" --appskey $abp_apps_key --nwkskey $abp_nwks_key --devaddr \"0228B1B1\" --decoder \"DecoderValueSensor\" --network \"$NETWORK\"
		# Add --tenant-id to add a tenant id
		./loradeviceprovisioning add --type otaa --deveui \"47AAC86800430028\" --appeui \"BE7A0000000014E2\" --appkey $otaa_key --decoder \"DecoderValueSensor\" --network \"$NETWORK\"
			
		echo \"The ABP device 46AAC86800430028 has an AppSKey of $abp_apps_key and a NwkSKey of $abp_nwks_key\"
		echo \"The OTAA device 47AAC86800430028 has an OTAA App key of $otaa_key\"
	fi
}

# Setting default values
# see https://www.gnu.org/software/bash/manual/html_node/Shell-Parameter-Expansion.html
STATION_DEVICE_NAME=${STATION_DEVICE_NAME:-AA555A0000000101}
# was EU863
REGION=${REGION:-US902}
NETWORK=${NETWORK-quickstartnetwork}
LNS_HOST_ADDRESS=${LNS_HOST_ADDRESS-ws://mylns:5000}
SPI_DEV=${SPI_DEV-0}
SPI_SPEED=${SPI_SPEED-8}

# Change this to pull our cli
LORA_CLI_URL='https://github.com/Azure/iotedge-lorawan-starterkit/releases/download/2.2.2/lora-cli.linux-musl-x64.tar.gz'
MONITORING_ENABLED=1
# Set this to the same name as a iotedge device created in iot hub.
EDGE_GATEWAY_NAME=
RESET_PIN=23
FACADE_SERVER_URL
FACADE_AUTH_CODE
IOTHUB_RESOURCE_ID
LOG_ANALYTICS_WORKSPACE_ID
LOG_ANALYTICS_SHARED_KEY
# the tag of the LNS docker image
LORA_VERSION=2.2.3


# deploy example device
DEPLOY_DEVICE=0


create_devices_with_lora_cli
