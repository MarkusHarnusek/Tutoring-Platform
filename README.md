# Tutoring Platform

## Overview
The Tutoring Platform is a comprehensive solution designed to streamline the management of tutoring sessions. It provides features for managing lessons, students, subjects, and schedules, ensuring an efficient and user-friendly experience for both tutors and students.

## Features
- **Student Management**: Add, update, and manage student information.
- **Lesson Scheduling**: Create and manage lesson schedules with configurable start times.
- **Subject Management**: Define and manage subjects with detailed descriptions.
- **Database Integration**: Synchronize in-memory data with a SQLite database.
- **Configurable Settings**: Load and apply configurations dynamically from a JSON file.
- **Logging**: Comprehensive logging for debugging and monitoring.
- **HTTPS Support**: Secure communication with HTTPS enabled.

## Project Structure
```
Tutoring-Platform/
├── deployment/          # Static website files
├── doc/                 # Documentation files
├── scripts/             # Deployment and setup scripts
├── server/              # Backend server code
│   ├── certs/           # SSL certificates
│   ├── config-objects/  # Configuration-related classes
│   ├── db-objects/      # Database entity classes
│   ├── request-objects/ # Request handling classes
│   └── ...              # Other server files
├── website/             # Frontend website files
└── README.md            # Project documentation
```

## Getting Started

### Prerequisites
- .NET 9.0 SDK
- SQLite 3.50.4 or higher
- A modern web browser

### Running the Application
1. Start the server:
   ```bash
   dotnet run
   ```
2. Open your browser and navigate to the frontend website in the `website` directory.

## Development
### Code Structure
- **Backend**: The `server` directory contains all backend logic, including database interactions and configuration handling.
- **Frontend**: The `website` directory contains static HTML, CSS, and JavaScript files for the user interface.
- **Scripts**: The `scripts` directory includes platform-specific scripts for setup and deployment.

## Acknowledgments
- **This project is still in a very active state of development, so don't expect everything to work straight away**
- Thanks to all contributors and users for their support.
- Special thanks to the open-source community for providing tools and libraries that made this project possible.
