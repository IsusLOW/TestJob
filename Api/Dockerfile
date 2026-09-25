FROM mcr.microsoft.com/dotnet/sdk:10.0

WORKDIR /src

COPY entrypoint.sh /entrypoint.sh
RUN chmod +x /entrypoint.sh

ENTRYPOINT ["/entrypoint.sh"]
