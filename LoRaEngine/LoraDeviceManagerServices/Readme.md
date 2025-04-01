## Building the docker image

First change directory: 

`cd lorawan\LoRaEngine\loradevicemanagerservices`

Then either 

`docker compose build loradevicemanagerservices`

or 

`docker compose up`

to run as daemon. Since restart: always, will restart automatically on reboot:

`docker compose up -d`
