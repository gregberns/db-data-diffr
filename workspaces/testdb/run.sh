#!/bin/bash

set -e

VERSION=$(cat ../../VERSION)

mkdir -p ./bin
cp ../../bin/$VERSION/db-data-diffr ./bin/db-data-diffr

docker-compose build

docker-compose up fcheck
