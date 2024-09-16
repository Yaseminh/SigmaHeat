# Use the official .NET 5 SDK image to build the application
FROM mcr.microsoft.com/dotnet/sdk:5.0 AS build-env

# Set the working directory inside the container
WORKDIR /app

# Copy the .csproj file and restore any dependencies
COPY DBClone/DBClone/*.csproj ./
RUN dotnet restore

# Copy the rest of the application code
COPY DBClone/. ./

# Build the application
RUN dotnet publish -c Release -o out


# Use the official .NET 5 runtime image to run the application
FROM mcr.microsoft.com/dotnet/aspnet:5.0

# Set the working directory inside the container
WORKDIR /app/out

# Copy the build output from the previous stage
COPY --from=build-env /app/out .




# Grant full permissions to the /app/out directory (equivalent to C:\Users\yasemin\Desktop\projeler\out on Windows)
RUN chmod -R 0777 /app/out

# Change to root user to execute the batch files
USER root

ENV PGPASSWORD=admin


# Expose the port that the application listens on
EXPOSE 80


# Define the entrypoint command to run the application
ENTRYPOINT ["dotnet", "DBClone.dll"]
