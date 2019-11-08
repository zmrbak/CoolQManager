using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CoolQManager
{
    public class ServiceConfig
    {
        public int ServicePort
        {
            set; get;
        }
        public String CommunicationKey
        {
            set; get;
        }
        public Boolean IsSslEnabled
        {
            set; get;
        }
        public Boolean IsFileSplited
        {
            set; get;
        }
    }
}
