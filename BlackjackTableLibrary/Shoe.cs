/*
 * Author: Tianyi Li
 * Date: 12/09/2020
 * Description: The Service Contract defined by ICallback and IShow interface. 
 *              Shoe class and helper methods to implement the game logic.
 */

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using System.ServiceModel;

namespace BlackjackTableLibrary
{
    [ServiceContract]
    public interface ICallback
    {
        [OperationContract(IsOneWay = true)]
        void UpdateGui(CallbackInfo info);
        [OperationContract(IsOneWay = true)]
        void UpdateNextPlayerInfo();
        [OperationContract(IsOneWay = true)]
        void UpdateCurrentPlayerInfo();
        [OperationContract(IsOneWay = true)]
        void UpdateFirstPlayerInfo();
        [OperationContract(IsOneWay = true)]
        void ShowResult(CallbackInfo info);
    }


    [ServiceContract(CallbackContract = typeof(ICallback))]
    public interface IShoe
    {
        [OperationContract]
        bool Join(string name);
        [OperationContract(IsOneWay = true)]
        void Leave(string name);
        [OperationContract]
        Card Deal();
        int NumCards { [OperationContract] get; }

        [OperationContract]
        Dictionary<string, List<Card>> GetPlayerCards();
        [OperationContract]
        List<string> GetPlayerList();
        [OperationContract(IsOneWay = true)]
        void Stand();
    }

    [ServiceBehavior(InstanceContextMode = InstanceContextMode.Single)]
    public class Shoe : IShoe
    {
        // Private attributes
        private Dictionary<string, ICallback> callbacks = new Dictionary<string, ICallback>();
        private List<Card> cards = null;
        private int cardIdx;

        private Dictionary<string, List<Card>> playerCards = new Dictionary<string, List<Card>>();

        // This list is for tracking the order of the players in the list to take turn.
        // Because dictionary is an unorder container
        private List<string> playerList = new List<string>();
        bool roundEnd = false;


        // Constructor
        public Shoe()
        {
            cards = new List<Card>();
            repopulate();
        }

        // Public methods & properties

        public void Shuffle()
        {
            Random rng = new Random();
            cards = cards.OrderBy(card => rng.Next()).ToList();
            cardIdx = 0;
        }

        public Card Deal()
        {
            if (cardIdx >= cards.Count())
                throw new IndexOutOfRangeException("The shoe is empty.");


            Card card = cards[cardIdx++];
            if (roundEnd)
            {
                playerCards["DEALER"].Insert(0, card);
                Console.WriteLine($"DEALER draw {card} from the pile.");
                updateAllClients();
                tellResultAllPlayers();
                return card;
            }


            ICallback cb = OperationContext.Current.GetCallbackChannel<ICallback>();
            // the player has already existed in callbacks
            var myKey = callbacks.FirstOrDefault(x => x.Value == cb).Key;
            if (myKey == null)
                myKey = "DEALER";
            if (playerCards.ContainsKey(myKey))
            {
                playerCards[myKey].Insert(0, card);
                Console.WriteLine($"{myKey} draw {card} from the pile.");
            }
            else
            {
                playerCards.Add(myKey, new List<Card>() { card });
                Console.WriteLine($"{myKey} draw {card} from the pile.");
            }


            // Initiate callbacks
            updateAllClients();

            return card;
        } // end Draw

        public int NumCards
        {
            get
            {
                // Returns the number of cards in the shoe that haven't aready 
                // been dealt via Draw()
                return cards.Count - cardIdx;
            }
        }

        private void repopulate()
        {
            // Clear out the "old" cards
            cards.Clear();

            // Add new "new" cards

            foreach (Card.SuitID s in Enum.GetValues(typeof(Card.SuitID)))
            {
                foreach (Card.RankID r in Enum.GetValues(typeof(Card.RankID)))
                {
                    cards.Add(new Card(s, r));
                }
            }

            // Randomize the collection
            Shuffle();
        }

        public bool Join(string name)
        {
            if (callbacks.ContainsKey(name.ToUpper()))
            {
                return false;
            }
            else if (callbacks.Count == 0) // if the first player sits down the table
            {
                // 'draw' two cards to dealer, draw method will add the cards to dictionary
                for (int i = 0; i < 2; i++)
                {
                    Card card = this.Deal();
                }

                // Retrieve client's callback proxy
                ICallback cb = OperationContext.Current.GetCallbackChannel<ICallback>();
                //Add dealer to list
                playerList.Add(name.ToUpper());
                // Save alias and callback proxy
                callbacks.Add(name.ToUpper(), cb);

                cb.UpdateFirstPlayerInfo();
                return true;
            }
            else
            {

                // Retrieve client's callback proxy
                ICallback cb = OperationContext.Current.GetCallbackChannel<ICallback>();

                //Add player
                playerList.Add(name.ToUpper());
                // Save alias and callback proxy
                callbacks.Add(name.ToUpper(), cb);

                // active the first player in a round when someone join in
                if (playerList.FindIndex(element => element == name.ToUpper()) == 1)
                {
                    callbacks[playerList[0]].UpdateFirstPlayerInfo();
                }

                var myKey = callbacks.FirstOrDefault(x => x.Value == cb).Key;
                Console.WriteLine($"{myKey} joined the table.");

                return true;
            }
        } // end Join

        public void Leave(string name)
        {
            if (callbacks.ContainsKey(name.ToUpper()))
            {
                //Remove player
                playerList.Remove(name.ToUpper());
                callbacks.Remove(name.ToUpper());
            }
        }


        // ******************   Helper methods **************************
        public Dictionary<string, List<Card>> GetPlayerCards()
        {
            return playerCards;
        }

        public List<string> GetPlayerList()
        {
            return playerList;
        }

        /*
         * In Backjack, when player click stand, then turn to the next player
         * so in this stand method, I check the previous player, current player and next player
         * in order to update different information
         * Every player including DEALER round once, because the first turn already deal two cards when they sit
         */
        public void Stand()
        {
            // Retrive current Callback
            ICallback cb = OperationContext.Current.GetCallbackChannel<ICallback>();
            // Get the name of the current Callback, the player name actually.
            var myKey = callbacks.FirstOrDefault(x => x.Value == cb).Key;

            // check if is the last player, if it is, deal cards to dealer after the last player
            if (myKey == playerList.Last())
            {
                roundEnd = true;
                int dealerPoints = 0;
                foreach (var item in playerCards["DEALER"])
                {
                    dealerPoints += Convert.ToInt16(item.Rank);
                }

                while (true)
                {
                    // Assuming the dealer will 'draw' a card if current points is less and equal 17
                    if (dealerPoints <= 17)
                    {
                        Card card = this.Deal();
                        dealerPoints += Convert.ToInt16(card.Rank);
                    }
                    else
                    {
                        updateAllClients();
                        tellResultAllPlayers();
                        break;
                    }
                }
                //return;
            }

            bool reachNext = false;
            string nextPlayer = "";

            foreach (string player in callbacks.Keys)
            {
                // this if execute only when the next if statement execute
                if (reachNext)
                {
                    nextPlayer = player; // set the next player
                    break;
                }
                if (player == myKey)
                    reachNext = true; // set reachNext true, so the next iterate will enter the if statement above
            }

            // this is current player, but actually it is previous player if turn to next player
            var prePlayer = "";
            foreach (string player in callbacks.Keys)
            {
                if (player == nextPlayer)
                {
                    callbacks[player].UpdateNextPlayerInfo(); // say 'it's your turn'
                    callbacks[prePlayer].UpdateCurrentPlayerInfo(); // say 'you're standing' to the previous player
                }
                prePlayer = player;
            }
        } // end Stand



        private void updateAllClients()
        {
            CallbackInfo info = new CallbackInfo(cards.Count - cardIdx, playerCards);

            foreach (ICallback cb in callbacks.Values)
                if (cb != null)
                    cb.UpdateGui(info);
        }

        private void tellResultAllPlayers()
        {
            CallbackInfo info = new CallbackInfo(cards.Count - cardIdx, playerCards);

            foreach (ICallback cb in callbacks.Values)
                if (cb != null)
                    cb.ShowResult(info);
        }

    } // end class
}
