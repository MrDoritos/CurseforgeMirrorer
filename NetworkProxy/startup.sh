#!/bin/bash
route del default gw 10.0.2.2
if [ $? -ne 0 ]; then
	echo "Failed to remove default gateway"
	exit 1
fi
ifconfig enp0s6u2 up
if [ $? -ne 0 ]; then
	echo "Failed to up enp0s6u2"
	exit 1
fi
dhclient enp0s6u2
if [ $? -ne 0 ]; then
	echo "DHCP failed on enp0s6u2"
	exit 1
fi
echo 65 > /proc/sys/net/ipv4/ip_default_ttl
if [ $? -ne 0 ]; then
	echo "Failed to set default TTL to 65"
	exit 1
fi
