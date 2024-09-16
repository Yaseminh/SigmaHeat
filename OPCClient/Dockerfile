# .NET 6 SDK imajını kullanarak build aşamasını oluşturun
FROM mcr.microsoft.com/dotnet/sdk:6.0 AS build-env

# Set the working directory inside the container
WORKDIR /app

# Copy the .csproj file and restore any dependencies
COPY OPCClient/OPCClient/*.csproj ./
RUN dotnet restore

# Copy the rest of the application code
COPY OPCClient/. ./

# Build the application
RUN dotnet publish -c Release -o out

# Küçük bir çalışma imajı oluşturun ve uygulamanızı bu imajda çalıştırın
FROM mcr.microsoft.com/dotnet/aspnet:6.0

# Set the working directory inside the container
WORKDIR /app/out

# Copy the build output from the previous stage
COPY --from=build-env /app/out .

# Copy SSL certificates from project directory
COPY OPCClient/myPrivateCert.pfx /app/myPrivateCert.pfx

# Set appropriate permissions for the certificate
RUN chmod 600 /app/myPrivateCert.pfx

# Expose the port that the application listens on
EXPOSE 443

# Define the entrypoint command to run the application
ENTRYPOINT ["dotnet", "OPCClient.dll"]
