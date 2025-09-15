#!/bin/bash

if [ -f /etc/os-release ]; then
    . /etc/os-release
    OS=$ID

    case "$OS" in
        ubuntu|debian)
            echo "Detected Debian-based system: $NAME"
            sudo apt update && sudo apt install -y autoconf automake autoconf-archive libtool perl \
            libx11-dev libxft-dev libxext-dev libwayland-dev libxkbcommon-dev libegl1-mesa-dev \
            libibus-1.0-dev
            ;;
        centos|rhel|fedora|athenaos)
            echo "Detected RHEL/Fedora-based system: $NAME"
            sudo dnf install -y autoconf automake autoconf-archive libtool perl libX11-devel \
            libXft-devel libXext-devel wayland-devel libxkbcommon-devel mesa-libEGL \
            ibus-devel
            ;;
        *)
            echo "Unknown or unsupported OS: $NAME"
            ;;
    esac
else
    echo "Cannot determine OS flavor (missing /etc/os-release)."
fi
