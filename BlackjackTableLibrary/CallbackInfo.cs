/*
 * Author: Tianyi Li
 * Date: 12/09/2020
 * Description: CallbackInfo class to define the DataContract.
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
    public class CallbackInfo
    {
        [DataMember]
        public int NumCards { get; private set; }
        [DataMember]
        public Dictionary<string, List<Card>> PlayerCards = null;
        public CallbackInfo(int c, Dictionary<string, List<Card>> d)
        {
            NumCards = c;
            PlayerCards = d;
        }
    }
}
