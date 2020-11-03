#!/bin/bash

VERSION=$(cat VERSION)
echo $VERSION

TAG=db-data-diffr:dotnet-$VERSION

echo "Start Build"
# docker build --target build -t $TAG . || exit $?
docker build -t $TAG . || exit $?
echo "End Build"

source_path=/app/db-data-diffr
destination_dir=./bin/$VERSION

mkdir -p $destination_dir

container_id=$(docker create $TAG)
docker cp $container_id:$source_path $destination_dir
docker rm $container_id
