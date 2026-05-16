# Carebed System

![CareBed UI Screenshot](./Carebed-main-UI.png)

Carebed is a modular, event-driven system designed for monitoring and managing sensors and actuators in a care environment. The system is built using .NET 8 and Windows Forms, with a focus on extensibility, testability, and clear separation of concerns. 

The main design goal with this system was to utilize an eventBus and very little coupling across modules. Each domain has a manager and that manager listens for message on the eventBus and delegates orders to the various workers it oversees.

In this way no system knows about any other system, beyond the message they receive across the eventBus. 

---

## Design Goals

- Decouple system components using event-driven communication
- Improve maintainability through modular architecture
- Support future scalability and integration
- Improve traceability through structured message handling

### Scalability Considerations

The event-driven architecture was designed to reduce coupling between components and support future expansion of sensors and system integrations.

---

## Features
- **Sensor Management:** Polls and aggregates data from multiple sensors.
- **Actuator Management:** Controls actuators such as motors or alarms.
- **Event Bus:** Decoupled event-driven communication between modules.
- **Message Envelopes:** Standardized message wrapping with metadata for routing and logging.
- **Extensible Architecture:** Easily add new sensors, actuators, or managers.
- **Unit Testing:** Includes a test project for core infrastructure and messaging components.

---

## Project Structure
- `Carebed/` - Main application (WinForms UI, managers, infrastructure)
- `Carebed.Tests/` - Unit tests for infrastructure and messaging
- `Class Sheets/` - Documentation for core classes and interfaces

---

## System Architecture

<img width="1600" height="900" alt="carebed_event_driven_architecture" src="https://github.com/user-attachments/assets/5e10eda5-df6e-4896-8451-3310f4e9c097" />

---

## Getting Started

### Prerequisites
- [.NET 8 SDK](https://dotnet.microsoft.com/en-us/download/dotnet/8.0)
- Windows OS (for WinForms support)

### Building the Project
1. Clone the repository:
   ```sh
   git clone https://github.com/Macroger/Carebed.git
   cd Carebed
   ```
2. Build the solution:
   ```sh
   dotnet build
   ```

### Running the Application
1. Navigate to the main project directory:
   ```sh
   cd Carebed
   ```
2. Run the application:
   ```sh
   dotnet run
   ```
   Or launch `Carebed.exe` from the build output directory.

### Running Tests
1. Navigate to the test project directory:
   ```sh
   cd Carebed.Tests
   ```
2. Run the tests:
   ```sh
   dotnet test
   ```

---

## Documentation
- See the `Class Sheets` folder for detailed documentation on core classes and interfaces.
- Design decisions and additional documentation are available in the Azure DevOps Wiki.

---

## Contributing
Contributions are welcome! Please open issues or submit pull requests for bug fixes, enhancements, or new features.

---

## What I Contributed

- Designed and implemented modular event-driven components
- Built message/event handling logic
- Added unit tests for core behaviour
- Helped document class responsibilities and architecture decisions

---

## Future Improvements

- Add actuator management
- Add persistent logging
- Expand unit test coverage
- Improve UI feedback for sensor states

---

## License
This project is licensed under the GNU General Public License v3.0. See the `LICENSE.txt` file for details.

---

**Copilot AI Acknowledgement:**
Some or all of this documentation was generated or assisted by GitHub Copilot AI.
