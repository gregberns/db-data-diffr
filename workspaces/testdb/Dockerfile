FROM debian:stretch as test

WORKDIR /app

# The curl and echoexport are for installing sqlcmd: https://docs.microsoft.com/en-us/sql/linux/sql-server-linux-setup-tools?view=sql-server-ver15#ubuntu
RUN apt-get update &&\
    apt-get install -y --no-install-recommends wget curl wdiff netcat ca-certificates gnupg2 apt-transport-https libicu-dev  &&\
    curl https://packages.microsoft.com/keys/microsoft.asc | apt-key add - &&\
    curl https://packages.microsoft.com/config/ubuntu/16.04/prod.list | tee /etc/apt/sources.list.d/msprod.list  &&\
    apt-get update  &&\
    ACCEPT_EULA=Y apt-get install -y --no-install-recommends mssql-tools  &&\
    apt-get install -y --no-install-recommends unixodbc-dev  &&\
    apt-get clean && rm -rf /var/lib/apt/lists/* && \
    echo 'export PATH="$PATH:/opt/mssql-tools/bin"' >> ~/.bashrc && \
    echo 'export PATH="$PATH:/opt/mssql-tools/bin"' >> ~/.bash_profile

RUN wget https://github.com/gregberns/fcheck/releases/download/0.3.1/fcheck --no-check-certificate && \
    mv ./fcheck /bin/fcheck &&\
    chmod +x /bin/fcheck

COPY ./bin/db-data-diffr .
RUN chmod +x ./db-data-diffr

# ENV DOTNET_SYSTEM_GLOBALIZATION_INVARIANT=false

CMD ["/bin/fcheck", "--config-file", "/config/fcheck-config.toml", "--report-file", "/output/report.json"]
