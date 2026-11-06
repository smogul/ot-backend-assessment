@echo off
REM Starts a local RabbitMQ on localhost with the login guest/guest.
REM The README links to this file. The real settings are in docker-compose.yml.
docker compose -f "%~dp0..\..\docker-compose.yml" up -d rabbitmq
