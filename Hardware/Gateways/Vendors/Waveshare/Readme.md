## Waveshare concentrator/BS (Raspberry PI hat)

### Hardware
- SX1302 915M LoRaWAN Gateway HAT [Purchase](https://www.amazon.com/dp/B0BGPZB4VW?ref=ppx_yo2ov_dt_b_fed_asin_title) [Doc](https://www.waveshare.com/wiki/SX1302_LoRaWAN_Gateway_HAT)
- Raspberry PI compute module IO Board [Purchase](https://www.amazon.com/dp/B08T259X2H?ref=ppx_yo2ov_dt_b_fed_asin_title)
- Raspberry PI compute model 4 [Purchase](https://www.amazon.com/dp/B0CN6T2RGV?ref=ppx_yo2ov_dt_b_fed_asin_title)

### Set up the hardware

- [Compute module setup instructions](https://www.waveshare.com/wiki/Write_Image_for_Compute_Module_Boards_eMMC_version)
	1. install rpiboot - gave us access from EMMC (flash drive) to USB
	2. run as admin
	3. put jumper on board 'boot'.
	4. Raspberry pi imager, installed ubuntu 24.04.1 LTS server.
- [Lorawan hat setup instructions](https://www.waveshare.com/wiki/SX1302_LoRaWAN_Gateway_HAT)

Reset pin: 23

### Configure Basics Station (otherwise known as the concentrator, or gateway, etc.)

[CLI Docs](https://azure.github.io/iotedge-lorawan-starterkit/2.2.2/user-guide/station-device-provisioning/)
1. Retrieve the Basics Station EUI in its hex-representation (e.g. AABBCCFFFE001122). 
2. use this EUI as the `--stationeui` when provisioning the BS using the CLI: `.\loradeviceprovisioning.exe add --type concentrator --stationeui AABBCCFFFE001122 --region US902 --no-cups --client-certificate-thumbprint <AABBCCFFFE001122.crt Thumbprint Here>`

#### Retrieving the EUI for a device running on a Linux machine:
Since we are running a dev kit on a Linux machine, this EUI can be retrieved from the MAC address of the eth0 interface as follows:

1. obtain the machine's ethernet address `cat /sys/class/net/eth0/address # prints the MAC Address of eth0`
2. insert the literals `FFFE` in the middle of this address, as per [Basic Station Glossary](https://doc.sm.tc/station/glossary.html?highlight=mac)

For example, if `aa:bb:cc:00:11:22` is the returned MAC address, the EUI will be `AABBCCFFFE001122`. 







