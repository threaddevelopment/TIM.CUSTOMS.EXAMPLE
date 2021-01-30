using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using TIM.CUSTOMS.EXAMPLE.Model.RequestModels;

namespace TIM.CUSTOMS.EXAMPLE.Executions
{
    public class GeneralExecution : IDisposable
    {
        public bool DoLogin(LoginModel model)
        {
            if (model.Username == "5" && model.Password == "5")
            {
                return true;
            }
            else
            {
                return false;
            }
        }
        public List<string> DoListWarehouses()
        {
            List<string> warehouses = new List<string>();
            warehouses.Add("WRHS1");
            warehouses.Add("WRHS2");
            warehouses.Add("WRHS3");
            warehouses.Add("WRHS4");
            warehouses.Add("WRHS5");
            warehouses.Add("WRHS6");
            return warehouses;
        }
        public void Dispose()
        {
            GC.SuppressFinalize(this);
        }
    }
}
