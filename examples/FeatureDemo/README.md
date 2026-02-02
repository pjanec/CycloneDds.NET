# FastCycloneDDS Feature Demo

A comprehensive showcase application for the **FastCycloneDDS C# Binding**, demonstrating high-performance patterns, zero-allocation architectures, and advanced DDS quality-of-service (QoS) features.

![FastCycloneDDS Demo](https://via.placeholder.com/800x400?text=FastCycloneDDS+Feature+Demo)

## 🚀 Overview

This console application is designed to verify the capabilities of the C# IDL compiler, runtime binding, and API surface. It allows you to run various scenarios in **Standalone** mode (single process) or **Distributed** mode (Master/Slave across network).

### Key Features Demonstrated
1.  **Chat Room (Basic Pub/Sub):** Simple reliable messaging with sender tracking.
2.  **Sensor Array (High Performance):** **Zero-Allocation** reading using `AsView()` API, achieving 100k+ msg/s with 0 GC pressure.
3.  **Stock Ticker (Content Filtering):** Efficient data selection using client-side predicates.
4.  **Flight Radar (Keyed Instances):** Managing keyed topics, O(1) instance lookup, and per-instance history.
5.  **Black Box (Durability):** Mission-critical data persistence (`TransientLocal`) allowing late-joining subscribers to receive historical logs.

---

## 🛠️ Usage

### Prerequisites
*   .NET 8.0 SDK
*   CycloneDDS native library (usually handled by the build output)

### Running the App
You can run the application directly from the `examples/FeatureDemo` folder:

```powershell
dotnet run
```

Running without arguments launches the **Interactive Menu**.

### Command Line Arguments
You can automate the mode selection using the `--mode` argument:

| Mode | Description |
| :--- | :--- |
| `standalone` | Runs Publisher and Subscriber in the SAME process. Best for quick local testing. |
| `master` | Acts as the **Control Node**. Shows the menu and commands Slave nodes to run scenarios. |
| `slave` | Acts as a **Subscriber Node**. Waits for commands from a Master. |
| `autonomous` | **"Attract Mode"**. Loops through all scenarios automatically (15s each). |

**Example:**
```powershell
# Run self-contained demo
dotnet run -- --mode autonomous
```

---

## 🧪 Scenarios in Detail

### 1. Chat Room
*   **Concept:** Standard reliable communication.
*   **Highlights:** Shows how easy the managed API is (`string` usage, simple loops).
*   **QoS:** `Reliable`, `PreserveOrder`.

### 2. Sensor Array (Zero-Copy)
*   **Concept:** High-frequency telemetry.
*   **Highlights:** Uses `FixedString32` and `View` structs to read data directly from native memory without creating C# objects.
*   **Metrics:** Displays live throughput and GC usage.

### 3. Stock Ticker
*   **Concept:** Market data feed.
*   **Highlights:** Subscriber applies a filter (e.g., `Symbol == "AAPL"`) to process only relevant data.

### 4. Flight Radar
*   **Concept:** Object tracking.
*   **Highlights:** Uses `[DdsKey]` attributes. Shows how to use `LookupInstance` to find specific flight history instantly (O(1)).

### 5. Black Box
*   **Concept:** System crash logging.
*   **Highlights:** Publisher writes logs and crashes (or stops). Subscriber joins *later* and still receives the critical history thanks to `TransientLocal` durability.

---

## 📂 Project Structure

*   `Schema.cs`: Defines all DDS Topics and QoS attributes.
*   `Scenarios/`: Contains the logic for each demo (Publisher/Subscriber pairs).
*   `Orchestration/`: Handles the Master/Slave control logic.
*   `UI/`: Spectre.Console shared components.

---

## 🤝 Distributed Mode (Master/Slave)

To test across two machines (or two terminals):

1.  **Terminal A:** `dotnet run -- --mode slave` (Waits for sync)
2.  **Terminal B:** `dotnet run -- --mode master` (Shows menu)

*Note: Ensure both share the same network and multicast allows discovery.*
