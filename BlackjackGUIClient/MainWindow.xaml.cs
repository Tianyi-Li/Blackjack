/*
 * Author: Tianyi Li
 * Date: 12/09/2020
 * Description: Client class
 */

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;

using System.ServiceModel;
using BlackjackTableLibrary;

namespace BlackjackGUIClient
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    [CallbackBehavior(ConcurrencyMode = ConcurrencyMode.Reentrant, UseSynchronizationContext = false)]
    public partial class MainWindow : Window, ICallback
    {
        // Member variables
        private IShoe shoe = null;

        public MainWindow()
        {
            InitializeComponent();
        }

        // This method is to tell the dealer draw a card to the current player.
        private void btnHit_Click(object sender, RoutedEventArgs e)
        {
            Card card = shoe.Deal();

            if (CalculatePoints() > 21)
            {
                MessageBox.Show("You Bust! Point is: " + CalculatePoints());

                // Update the current player info
                myLstCards.Items.Insert(0, card);
                btnHit.IsEnabled = btnStand.IsEnabled = false;
                UpdateCurrentPlayerInfo(CalculatePoints());

                // Call the stand method to invoke next player, because the current player Bust.
                shoe.Stand();
            }
            else
            {
                // Update the current player info and shoe cards count
                myLstCards.Items.Insert(0, card);
                UpdateCurrentPlayerInfo(CalculatePoints());
            }
        }

        // Set a player name
        private void buttonSet_Click(object sender, RoutedEventArgs e)
        {
            if (txtAlias.Text != "")
            {
                try
                {
                    btnSet.IsEnabled = txtAlias.IsEnabled = false;
                    connectToCardTable();
                }
                catch (Exception ex)
                {
                    MessageBox.Show(ex.Message);
                }
            }
        }

        // Stand method means that the current player don't need more cards.
        // Take turn to the next player or to the dealer when the current player is the last player in a round
        private void btnStand_Click(object sender, RoutedEventArgs e)
        {
            btnHit.IsEnabled = btnStand.IsEnabled = false;
            shoe.Stand();
        }

        private void Window_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            if (shoe != null)
                shoe.Leave(txtAlias.Text.ToUpper());
        }

        // *******************  Helper methods    **********************

        private void connectToCardTable()
        {
            try
            {
                // Connect to the Shoe service using DUPLEX channel (for callbacks)
                DuplexChannelFactory<IShoe> channel = new DuplexChannelFactory<IShoe>(this, "ShoeEndPoint");
                shoe = channel.CreateChannel();

                if (shoe.Join(txtAlias.Text))
                {
                    // Deal two cards to each player and the dealer in the first round
                    for (int i = 0; i < 2; i++)
                    {
                        Card card = shoe.Deal();

                        // Update the current player info
                        myLstCards.Items.Insert(0, card);
                    }

                    UpdateCurrentPlayerInfo(CalculatePoints());

                    txtAlias.IsEnabled = btnSet.IsEnabled = false;
                }
                else
                {
                    // Alias rejected by the service so nullify service proxies
                    shoe = null;
                    txtAlias.IsEnabled = btnSet.IsEnabled = true;
                    MessageBox.Show("ERROR: Alias in use. Please try again.");
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message);
            }
        }

        // Update the current player's hand card, points and shoe cards cound
        private void UpdateCurrentPlayerInfo(int points)
        {
            shoeLbl.Content = shoe.NumCards.ToString();
            myPoints.Content = $"{points} ";
        }

        // Calculate current player's total points
        private int CalculatePoints()
        {
            // Get all player cards
            Dictionary<string, List<Card>> playerCards = shoe.GetPlayerCards();
            // Get the current player cards
            List<Card> listCards = playerCards[txtAlias.Text.ToUpper()];
            int points = 0;

            foreach (Card card in listCards)
            {
                points += Convert.ToInt16(card.Rank);
            }
            return points;
        }

        private int CalculateDealerPoints()
        {
            Dictionary<string, List<Card>> playerCards = shoe.GetPlayerCards();
            List<Card> listCards = playerCards["DEALER"];
            int points = 0;
            foreach (Card card in listCards)
            {
                points += Convert.ToInt16(card.Rank);
            }
            return points;
        }
        private int CalculateDealerPointsYouCanSee()
        {
            Dictionary<string, List<Card>> playerCards = shoe.GetPlayerCards();
            List<Card> listCards = playerCards["DEALER"];
            int points = 0;
            for (int i = 0; i < listCards.Count; i++)
            {
                if (i == listCards.Count-1)
                    break;
                else
                    points += Convert.ToInt16(listCards[i].Rank);
            }
            
            return points;
        }


        private delegate void ClientUpdateDelegate(CallbackInfo info);

        public void UpdateGui(CallbackInfo info)
        {
            if (System.Threading.Thread.CurrentThread == this.Dispatcher.Thread)
            {
                // Update the shoe card counts
                shoeLbl.Content = info.NumCards.ToString();

                // Get all player cards
                Dictionary<string, List<Card>> playerCards = info.PlayerCards;

                otherPlayerLst.Items.Clear();
                dealer.Items.Clear();

                foreach (string playerName in playerCards.Keys)
                {
                    // Update other players in this table, except the current player and dealer
                    // Because we can see our own cards in hand
                    if (playerName != txtAlias.Text.ToUpper() && playerName != "DEALER")
                    {
                        foreach (var card in playerCards[playerName])
                        {
                            otherPlayerLst.Items.Add("[" + playerName + "] " + card);
                        }
                    }
                    else if (playerName == "DEALER")
                    {
                        for (int i = 0; i < playerCards[playerName].Count; i++)
                        {
                            if (i == playerCards[playerName].Count-1)
                                dealer.Items.Add("Hidden!");
                            else
                                dealer.Items.Add("[" + playerName + "] " + playerCards[playerName][i]);
                        }
                        
                        // Update dealer points
                        dealerPoints.Content = $"{CalculateDealerPointsYouCanSee()}";
                    }
                    else
                        continue;
                }
            }
            else
                this.Dispatcher.BeginInvoke(new ClientUpdateDelegate(UpdateGui), info);
        }


        private delegate void NextClientDelegate();
        public void UpdateNextPlayerInfo()
        {
            if (System.Threading.Thread.CurrentThread == this.Dispatcher.Thread)
            {
                infoLbl.Content = "Your turn!";
                btnHit.IsEnabled = btnStand.IsEnabled = true;
                // Shake the MainWindow to tell the player that it's your turn
                NudgeWindow(this);
            }
            else
                this.Dispatcher.BeginInvoke(new NextClientDelegate(UpdateNextPlayerInfo));
        }

        private delegate void CurrentClientDelegate();
        public void UpdateCurrentPlayerInfo()
        {
            if (System.Threading.Thread.CurrentThread == this.Dispatcher.Thread)
            {
                if (CalculatePoints() > 21)
                    infoLbl.Content = $"You Bust! Point is: {CalculatePoints()}";
                else
                {
                    infoLbl.Content = "Wait other turns";
                }
            }
            else
                this.Dispatcher.BeginInvoke(new CurrentClientDelegate(UpdateCurrentPlayerInfo));
        }

        private delegate void FirstClientDelegate();
        public void UpdateFirstPlayerInfo()
        {
            if (System.Threading.Thread.CurrentThread == this.Dispatcher.Thread)
            {

                List<string> playerList = shoe.GetPlayerList();
                // When someone join in, active the first player in a round.
                if (playerList.Count == 2)
                {
                    infoLbl.Content = "Start from you";
                    btnHit.IsEnabled = btnStand.IsEnabled = true;
                }
                else
                {
                    infoLbl.Content = "Wait other players join in (2-8 players)";
                    btnHit.IsEnabled = btnStand.IsEnabled = false;
                }
            }
            else
                this.Dispatcher.BeginInvoke(new FirstClientDelegate(UpdateFirstPlayerInfo));
        }

        private delegate void PlayerResultDelegate(CallbackInfo info);

        // Shoe who win or Lost when end game
        public void ShowResult(CallbackInfo info)
        {
            if (System.Threading.Thread.CurrentThread == this.Dispatcher.Thread)
            {
                Dictionary<string, List<Card>> playerCards = info.PlayerCards;
                int dealerPoints_ = CalculateDealerPoints();
                int currentPlayerPoints = 0;

                foreach (var card in playerCards[txtAlias.Text.ToUpper()])
                {
                    currentPlayerPoints += Convert.ToInt16(card.Rank);
                }

                // if current player points is over than 21, don't need compare to other, just Lost this game
                if (currentPlayerPoints > 21)
                    infoLbl.Content = "You Bust!";
                else if (dealerPoints_ > 21)
                    infoLbl.Content = "Congrats! You Win! Beacuse Dealer Bust!";
                else if (currentPlayerPoints <= 21 && currentPlayerPoints > dealerPoints_)
                    infoLbl.Content = "You Win!";
                else if (currentPlayerPoints == dealerPoints_)
                    infoLbl.Content = $"Push!. Dealer has the same {dealerPoints_} points as you. No one wins";
                else
                    infoLbl.Content = $"You Lost! Dealer has {dealerPoints_} points. You has {currentPlayerPoints}";
                
                dealer.Items.Clear();
                foreach (var card in playerCards["DEALER"])
                {
                    dealer.Items.Add("[" + "DEALER" + "] " + card);
                }
                dealerPoints.Content = $"{dealerPoints_}";
            }
            else
                this.Dispatcher.BeginInvoke(new PlayerResultDelegate(ShowResult), info);
        }

        // This is a method that shake the MainWindow
        // Reference from https://stackoverflow.com/questions/23687318/nudge-wpf-window
        public void NudgeWindow(Window window)
        {
            var maxOffset = 9;
            var minOffset = 1;
            var originalLeft = (int)window.Left;
            var originalTop = (int)window.Top;
            var rnd = 0;

            var RandomClass = new Random();
            for (int i = 0; i <= 500; i++)
            {
                rnd = RandomClass.Next(originalLeft + minOffset, originalLeft + maxOffset);
                window.Left = rnd;
                rnd = RandomClass.Next(originalTop + minOffset, originalTop + maxOffset);
                window.Top = rnd;
            }
            window.Left = originalLeft;
            window.Top = originalTop;
        }


    } // end MainWindow
}
