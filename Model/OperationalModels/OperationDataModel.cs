using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using TIM.CUSTOMS.EXAMPLE.Model.RequestModels;

namespace TIM.CUSTOMS.EXAMPLE.Model.OperationalModels
{
    public class OperationDataModel:LoginModel
    {
        public string Warehouse { get; set; }
        public string FromLocation { get; set; }
        public string ToLocation { get; set; }

    }
}
