# Cheatmate - AI-Powered Chess Training & Analysis Desktop App

[![GitHub Repo](https://img.shields.io/badge/GitHub-Repository-blue?logo=github)](https://github.com/cyber-dev-skalovsi/SchachAnalyseGUI)  
[![Language](https://img.shields.io/badge/language-C%23-blue)](https://docs.microsoft.com/en-us/dotnet/csharp/)

Chess training application with AI-powered move explanations, real-time position analysis, and educational insights. Built for chess students, coaches, and enthusiasts who want to understand chess positions deeply through natural language explanations.

<img width="431" height="477" alt="image" src="https://github.com/user-attachments/assets/5dd8889b-a56c-424a-9bc2-18c7dde35917" />
<img width="239" height="459" alt="image" src="https://github.com/user-attachments/assets/717dd21d-b507-4ae2-8261-3da8da9a3256" />
<img width="242" height="493" alt="image" src="https://github.com/user-attachments/assets/e30a73d6-bfbd-48bb-8e95-fe31dc1987ff" />

## Core Features

### AI-Powered Learning
- **Natural Language Explanations**: Groq-powered AI explains every move in clear, educational language
- **Mistake Analysis**: Automatic detection of tactical and positional errors with detailed breakdowns
- **Strategic Insights**: Learn what moves accomplish, including follow-up plans and tactical patterns
- **Contextual Teaching**: Different explanations for captures, positional moves, castling, and promotions

### Position Analysis Engine
- **Custom Chess Engine**: Built-in minimax algorithm with alpha-beta pruning for move suggestions
- **Evaluation Tracking**: Monitors position scores to help understand advantage shifts
- **Move Comparison**: Analyzes the difference between played moves and engine recommendations
- **Material & Positional Assessment**: Real-time evaluation based on piece values and board control

### Interactive Training Interface
- **Visual Board Display**: Clear chessboard visualization with Unicode pieces
- **Move Highlighting**: Yellow squares show suggested moves for easy visualization
- **Adjustable Display**: Customizable window size and transparency to fit your workflow
- **Board Orientation**: Switch between white and black perspective

### Study Tools
- **PGN Import**: Load games via HTTP endpoint or direct input
- **Position Setup**: Analyze any position using standard PGN notation
- **Evaluation Breakdown**: Detailed scoring components for educational purposes
- **Export Functionality**: Save analysis for review and study

## Educational Use Cases

- **Post-Game Analysis**: Review your completed games and understand mistakes
- **Opening Study**: Explore opening variations with AI explanations
- **Tactical Training**: Practice finding the best moves in tactical positions
- **Chess Coaching**: Use as a teaching tool to explain positions to students
- **Position Understanding**: Learn strategic concepts through detailed explanations

## How It Works

1. **Import Position**: Send PGN notation via HTTP POST to `localhost:30012`
2. **Engine Analysis**: Custom chess engine evaluates position and suggests optimal moves
3. **Evaluation Assessment**: Calculates position score and identifies tactical opportunities
4. **AI Explanation**: Groq API generates educational explanations in plain language
5. **Visual Feedback**: Displays analysis on interactive chessboard with highlighted suggestions

## Setup Instructions

### Prerequisites
- .NET Framework 4.8 or higher
- Windows 10/11
- Groq API key (optional, for AI explanations - free tier available)

### Installation

1. **Clone Repository:**
   ```bash
   git clone https://github.com/cyber-dev-skalovsi/SchachAnalyseGUI.git
   cd SchachAnalyseGUI
   ```

2. **Configure API Key** (Optional for AI features):
   - Open `GroqAIHelper.cs`
   - Add your Groq API key in the `GROQ_API_KEY` field
   - Get free API key at: https://console.groq.com
   - Engine analysis works without API key

3. **Build Project:**
   ```bash
   dotnet build -c Release
   ```

4. **Launch Application:**
   ```bash
   cd bin/Release
   ./ChessMentor.exe
   ```

### Usage

**Keyboard Shortcuts:**
- `Ctrl+Shift+H`: Show/hide analysis window
- `Ctrl+Shift+F`: Flip board orientation
- `Ctrl+Shift+O`: Open settings menu
- `Ctrl+Shift+X`: Exit application
- `Arrow Keys`: Reposition window

**Import Position via API:**
```http
POST http://localhost:30012/
Content-Type: application/json

{
  "position": "[PGN notation here]"
}
```

**Example Request:**
```bash
curl -X POST http://localhost:30012/ \
  -H "Content-Type: application/json" \
  -d '{"position": "1. e4 e5 2. Nf3 Nc6 3. Bb5"}'
```

## Project Structure

```
ChessMentor/
├── MainWindow.xaml           # GUI layout and styling
├── MainWindow.xaml.cs         # Main application logic and HTTP listener
ChessChallenge/API/
├── GroqAIHelper.cs            # AI explanation integration
├── MyBot.cs                   # Chess engine (minimax algorithm)
├── MyBotAnalyzer.cs           # Position analysis utilities
├── EvalBreakdown.cs           # Evaluation component details
├── EvalExporter.cs            # Analysis export functionality
└── BitboardHelper.cs          # Chess bitboard operations
```

## Technologies

- **C# / .NET Framework**: Core application and WPF GUI
- **Custom Chess Engine**: Minimax with alpha-beta pruning (configurable depth)
- **Groq API**: LLaMA 3.3 70B for natural language explanations
- **System.Net.HttpListener**: Local HTTP server for position input
- **WPF**: Modern Windows desktop interface
- **JSON**: Position data serialization

## Configuration

### Settings Menu Options
- **Window Opacity**: 0.1 to 1.0 (adjust transparency)
- **Window Width**: 100px to 400px (resize interface)
- **Engine Toggle**: Enable/disable move suggestions
- **AI Explanations**: Enable/disable natural language analysis

### Advanced Customization
Edit source code to adjust:
- Engine search depth (default: 6 plies for balanced speed/strength)
- Piece evaluation values
- Explanation detail level
- Server port (default: 30012)

## Example Analysis Output

**Opening Move:**
```
Suggested Move: e2-e4 | Analysis Time: 150ms | Evaluation: +0.2

Opens lines for the bishop and queen while controlling the center. 
This flexible pawn move allows for various opening systems and puts 
immediate pressure on Black's position.
```

**Tactical Position:**
```
Suggested Move: Qd1-h5 | Analysis Time: 220ms | Evaluation: +2.8

This move attacks the undefended knight on f7 and threatens checkmate. 
After the knight moves, White can capture on f7 with check, winning 
material and disrupting Black's king safety.
```

## Contributing

Contributions welcome! Ideas for enhancement:
- Stockfish UCI engine integration for stronger analysis
- Opening book database for theory study
- Puzzle mode for tactical training
- Multi-language support
- Game database import/export
- Performance optimizations

## Roadmap

- [ ] Integration with popular chess websites (chess.com, lichess.org)
- [ ] Save/load analysis sessions
- [ ] Variation explorer for opening study
- [ ] Position search in game databases
- [ ] Mobile companion app

## License

This project is open-source and available for educational purposes. Contributions and feedback are encouraged.

## Acknowledgments

- Chess Challenge API for board representation framework
- Groq for AI inference infrastructure
- The chess programming community for algorithms and techniques

**Educational Tool Notice**: ChessMentor is designed for post-game analysis, position study, and chess education. This tool helps players understand chess principles and improve their game through detailed analysis and AI-powered explanations.
