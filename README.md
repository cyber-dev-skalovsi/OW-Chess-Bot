# Cheatmate - Hidden Chess Assistant with AI Analysis

[![GitHub Repo](https://img.shields.io/badge/GitHub-Repository-blue?logo=github)](https://github.com/cyber-dev-skalovsi/SchachAnalyseGUI)  
[![Language](https://img.shields.io/badge/language-C%23-blue)](https://docs.microsoft.com/en-us/dotnet/csharp/)

A sophisticated chess analysis overlay with AI-powered explanations, real-time blunder detection, and complete stealth capabilities. Built for tournament preparation, training analysis, and position evaluation with natural language insights.

<img width="431" height="477" alt="image" src="https://github.com/user-attachments/assets/5dd8889b-a56c-424a-9bc2-18c7dde35917" />
<img width="239" height="459" alt="image" src="https://github.com/user-attachments/assets/717dd21d-b507-4ae2-8261-3da8da9a3256" />
<img width="242" height="493" alt="image" src="https://github.com/user-attachments/assets/e30a73d6-bfbd-48bb-8e95-fe31dc1987ff" />

## Core Features

### Real-Time AI Analysis
- **Natural Language Explanations**: Groq-powered AI explains every move in 2-3 concise sentences
- **Blunder Detection**: Automatic detection when opponent's moves swing evaluation by +1.0 or more
- **Strategic Insights**: Explains what the move accomplishes, follow-up plans, and tactical threats
- **Contextual Analysis**: Differentiates between normal moves, captures, castling, and promotions

### Intelligent Position Evaluation
- **Evaluation Tracking**: Monitors position scores before and after each move
- **Opponent Move Quality**: Compares positions to identify mistakes and opportunities
- **Material-Based Scoring**: Real-time evaluation using piece values and positional factors
- **Blunder Alerts**: Visual warnings with advantage calculations when opponent makes critical errors

### Advanced Overlay System
- **Semi-Transparent Display**: Adjustable opacity (0.1-1.0) for minimal screen obstruction
- **Auto-Expanding Panel**: Explanation box grows with content, no scrolling needed
- **Move Highlighting**: Yellow squares mark the suggested move's origin and destination
- **Resizable Interface**: Width adjustment from 100-400px to fit any screen layout

### Stealth Capabilities
- **Process Masking**: Runs as `svchost.exe` to avoid detection in Task Manager
- **Click-Through Mode**: Overlay doesn't interfere with underlying applications
- **Global Hotkeys**: Control without bringing window to foreground
- **Minimal Footprint**: Lightweight HTTP server on localhost:30012

### Configuration Options
- **API Connection Toggle**: Enable/disable bot analysis without closing application
- **AI Explanations Toggle**: Keep bot running but disable expensive API calls
- **Opacity Control**: Fine-tune transparency for your environment
- **Width Adjustment**: Customize overlay size for optimal viewing
- **Position Locking**: Prevent accidental movement during critical games

## How It Works

1. **Position Input**: Sends PGN via HTTP POST to `localhost:30012`
2. **FEN Conversion**: Converts PGN notation to FEN for engine processing
3. **Bot Analysis**: Custom minimax algorithm evaluates position and suggests best move
4. **Evaluation Comparison**: Tracks score changes to detect blunders
5. **AI Explanation**: Groq API generates natural language analysis (if enabled)
6. **Visual Display**: Updates chessboard with highlighted move and explanation text

## Setup Instructions

### Prerequisites
- .NET Framework 4.8 or higher
- Windows 10/11
- Admin privileges (for global hotkeys and stealth features)
- Groq API key (optional, for AI explanations)

### Installation

1. **Clone Repository:**
   ```
   git clone https://github.com/cyber-dev-skalovsi/Cheatmate.git
   cd SchachAnalyseGUI
   ```

2. **Configure API Key** (Optional):
   - Open `GroqAIHelper.cs`
   - Replace API key in `GROQ_API_KEY` field
   - Or disable AI explanations in options menu
   (only if you want your own kind of AI model to be used)

3. **Build Project:**
   ```
   dotnet build -c Release
   ```

4. **Launch Application:**
   ```
   cd bin/Release
   ./Cheatmate.exe
   ```

### Usage

**Hotkeys:**
- `Ctrl+Shift+H`: Toggle overlay visibility
- `Ctrl+Shift+F`: Flip board orientation
- `Ctrl+Shift+L`: Lock/unlock position
- `Ctrl+Shift+O`: Open options menu
- `Ctrl+Shift+X`: Close application
- `Arrow Keys`: Reposition overlay (when unlocked)

**API Endpoint:**
```
POST http://localhost:30012/
Content-Type: application/json

{
  "position": "[PGN notation here]"
}
```

**Example Request:**
```
curl -X POST http://localhost:30012/ \
  -H "Content-Type: application/json" \
  -d '{"position": "1. e4 e5 2. Nf3 Nc6"}'
```

## Project Structure

```
Cheatmate/
├── MainWindow.xaml           # GUI layout and styling
├── MainWindow.xaml.cs         # Main application logic and HTTP listener
ChessChallenge/API/
├── GroqAIHelper.cs            # AI explanation integration
├── MyBot.cs                   # Chess engine (minimax algorithm)
├── MyBotAnalyzer.cs           # Position analysis utilities
├── EvalBreakdown.cs           # Evaluation component breakdown
├── EvalExporter.cs            # Analysis export functionality
└── BitboardHelper.cs          # Chess bitboard operations
```

## Technologies

- **C# / .NET Framework**: Core application and WPF GUI
- **Custom Chess Engine**: Minimax algorithm with alpha-beta pruning
- **Groq API**: LLaMA 3.3 5.5B for fast responses (up to 1ms)
- **System.Net.HttpListener**: Local HTTP server for position input
- **WinAPI Interop**: Global hotkeys and window management
- **JSON Parsing**: Position data serialization/deserialization

## Configuration

### Options Menu Settings
- **Opacity**: 0.1 to 1.0 (adjustable in 0.1 increments)
- **Width**: 100px to 400px (adjustable in 10px increments)
- **API Connection**: Enable/disable bot analysis
- **AI Explanations**: Enable/disable Groq API calls

### Advanced Settings
Edit source code to customize:
- Bot search depth (default: 6 plies)
- Blunder threshold (default: 1.0 pawn advantage)
- Explanation length (default: 2-3 sentences)
- Evaluation piece values
- Port number (default: 30012)

## Example Output

**Normal Move:**
```
Move: e2e4 | 150ms
Advances the central pawn to control key squares and open lines 
for the bishop and queen. This prepares for rapid piece development 
while maintaining flexibility in the opening.
```

**Blunder Detection:**
```
BLUNDER! +2.3 | Move: d1h5 | 220ms | Captures Queen
Opponent left their queen undefended on h5, allowing a free capture 
worth 9 points of material. Follow up by consolidating the advantage 
with Nc3 to develop while maintaining the extra queen.
```

## Screenshots

Place your screenshots in the following locations:

```
docs/screenshots/
├── gui-overlay.png          # Main screenshot (required)
├── options-menu.png         # Options configuration panel
├── blunder-alert.png        # Example of blunder detection
├── normal-analysis.png      # Regular move explanation
└── stealth-mode.png         # Overlay with click-through enabled
```

## Contributing

Contributions are welcome! Areas for improvement:
- Integration with Stockfish or other UCI engines
- Opening book database
- Endgame tablebase support
- Multi-language support for explanations
- Custom evaluation function tuning
- Performance optimization for deeper searches

## Acknowledgments

- Chess Challenge API for board representation
- Groq for AI inference infrastructure
- Community contributors and testers

---

The use of CHEATMATE is at your own risk. The tool is provided for educational and demonstration purposes only. 
CHEATMATE assumes no liability for damages or violations that may arise from the use. There are no claims as to the function, security or integrity of the software.
