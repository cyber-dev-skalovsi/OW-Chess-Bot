using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;

namespace ChessBot 
{
    public partial class MainWindow : Window  // "partial" ist entscheidend!
    {
        public MainWindow()
        {
            InitializeComponent();  // Das hier muss vor der Nutzung von ChessBoard stehen!
            DrawBoard();
        }

        private void DrawBoard()
        {
            if (ChessBoard == null)
            {
                MessageBox.Show("ChessBoard ist null – XAML nicht geladen!");
                return;
            }

            for (int row = 0; row < 8; row++)
            {
                for (int col = 0; col < 8; col++)
                {
                    var square = new Rectangle  // Besser als Image für einfache Quadrate
                    {
                        Width = 75,
                        Height = 75,
                        Fill = new SolidColorBrush((row + col) % 2 == 0 ?
                            Colors.LightGray : Colors.DarkGray)  // Alternierende Farben
                    };
                    ChessBoard.Children.Add(square);
                }
            }
        }
    }
}