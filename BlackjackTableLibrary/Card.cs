/*
 * Author: Tianyi Li
 * Date: 12/09/2020
 * Description: Card class
 */

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using System.Runtime.Serialization;

namespace BlackjackTableLibrary
{
    [DataContract]
    public class Card
    {
        public enum SuitID { Clubs, Diamonds, Hearts, Spades };
        public enum RankID { Ace = 1, Two, Three, Four, Five, Six, Seven, Eight, Nine, Ten, King = 10, Queen = 10, Jack = 10 };

        // Public methods & properties
        [DataMember]
        public SuitID Suit { get; private set; }
        [DataMember]
        public RankID Rank { get; private set; }
        public override string ToString()
        {
            // Show number instead of word, makes people see straightforward
            return Convert.ToInt16(Rank).ToString() + " of " + Suit.ToString();
        }

        // Constructor
        internal Card(SuitID s, RankID r)
        {
            Suit = s;
            Rank = r;
        }
    }
}
