# Cheatmate - Hidden Chess Assistant

[![GitHub Repo](https://img.shields.io/badge/GitHub-Repository-blue?logo=github)](https://github.com/cyber-dev-skalovsi/SchachAnalyseGUI)  
[![License: MIT](https://img.shields.io/badge/License-MIT-yellow.svg)](https://opensource.org/licenses/MIT)  
[![Language](https://img.shields.io/badge/language-C%23-blue)](https://docs.microsoft.com/en-us/dotnet/csharp/)   
**Development Period:** 2021–2025  
**Focus:** Real-time analysis, GUI apps, game design  

Completely custom-built for discreet chess analysis, with stealth features for tournaments or training. Processes PGN positions via HTTP and suggests moves from a local AI bot. Modular and open-source—extend with Stockfish for deeper play.

### ⚙️ How to Use:
**Prerequisites:** .NET Framework 4.8+, Windows 10/11. Run as admin for full stealth/hotkeys.

1. **Clone & Build:**  
   ```
   git clone https://github.com/cyber-dev-skalovsi/SchachAnalyseGUI.git
   cd SchachAnalyseGUI
   dotnet build -c Release
   ```

2. **Launch:** Run `Cheatmate.exe`. HTTP server starts on port 30012.

3. **Activate Overlay:** Press **Right Shift** (or Ctrl+Q) to toggle.  
   Send PGN via POST to `http://localhost:30012/analyze` for instant suggestions.

**Pro Tip:** Hides as `svchost.exe`—invisible in Task Manager for ultimate stealth.

## Key Features
- **Real-time PGN Processing:** HTTP listener with Unicode board, white/black toggle, best-move highlight.  
- **AI Bot:** Custom Minimax (MyBot) with explanations (e.g., "Captures knight, check!").  
- **Stealth Mode:** Click-through, hotkeys (arrows to reposition), semi-transparent overlay.  
- **Robust:** Error-handled PGN-to-FEN converter; CORS for web tools.

## Technologies
- C# (.NET/WPF GUI)  
- Custom C# Chess Engine (Minimax logic)  
- `System.Net.Http` (Server)  
- WinAPI (Hotkeys/Process Hiding)  
- JSON Parsing  
