using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using TIM.CUSTOMS.EXAMPLE.Executions;
using TIM.CUSTOMS.EXAMPLE.Model.OperationalModels;
using TIM.CUSTOMS.EXAMPLE.Model.RequestModels;
using TIM.SDK.GLOGI;
using TIM.SDK.GLOGI.Executions;
using TIM.SDK.GLOGI.Helpers;
using TIM.SDK.GLOGI.Models;

namespace TIM.CUSTOMS.EXAMPLE.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class GlogiController : Controller
    {
        private string _cacheUrl { get; set; }
        public Indicator _indicator { get; set; }

        private readonly IConfiguration Configuration;
        public GlogiController(IConfiguration configuration)
        {
            Configuration = configuration;

            _cacheUrl = Configuration["CacheUrl"];


            if (_indicator == null)
                _indicator = DoInitIndicator();
        }

        /// <summary>
        /// Get Method is the first call method for device
        /// </summary>
        /// <param name="id">Mac address of the device</param>
        /// <returns>Full display of device</returns>
        [HttpGet]
        [Route("DeviceCall")]
        public Display DeviceCall(string id)
        {
            try
            {
                var cacheKey = "OperationKey_" + id;
                var currentStateData = StateFactory<OperationDataModel>.GetCurrentState(_cacheUrl, cacheKey);
                if (currentStateData != null)
                    return currentStateData.Response;
                StateFactory<OperationDataModel>.InitializeStateMachine(_cacheUrl, cacheKey, DateTime.Now.AddMinutes(10), id, "GET");
                var screen = DisplayFactory.InitializeScreen("loginscreen", "Login");
                using (Display display = DisplayFactory.InitializeDisplay(DisplayMode.BarcodeScan, 0, screen, _indicator))
                {
                    DisplayFactory.AddScreenElement(screen, DisplayFactory.CreateScreenElement(ScreenElementType.Text, null, null, null, "Username", null, Font.M, Alignment.Center, null), 1);
                    DisplayFactory.AddScreenElement(screen, DisplayFactory.CreateScreenElement(ScreenElementType.Input, "txtusername", null, null, null, null, Font.M, Alignment.Center, null), 2);
                    DisplayFactory.AddScreenElement(screen, DisplayFactory.CreateScreenElement(ScreenElementType.Text, null, null, null, "Password", null, Font.M, Alignment.Center, null), 3);
                    DisplayFactory.AddScreenElement(screen, DisplayFactory.CreateScreenElement(ScreenElementType.Input, "txtpassword", null, null, null, null, Font.M, Alignment.Center, null), 4);
                    StateFactory<OperationDataModel>.AddOrUpdateStateMachineState(_cacheUrl, cacheKey, new StateModel { DoNotCall = false, Request = null, Response = display, ScreenId = "GET" }, true, DateTime.Now.AddMinutes(10));
                    return display;
                }
            }
            catch (ScreenException ex)
            {
                return DisplayFactory.CreateException(ex.State, ex.Header, ex.ErrorMessage, ButtonTypes.exception);
            }
        }

        [HttpPost]
        [Route("DeviceCall")]
        public Display DeviceCall([FromBody] DeviceRequest model)
        {
            try
            {
                string eventName = model.data.@event;
                var id = model.device.id;
                var cacheKey = "OperationKey_" + id;
                var stateMachine = StateFactory<OperationDataModel>.GetStateMachine(_cacheUrl, cacheKey);
                OperationDataModel operationalData = StateFactory<OperationDataModel>.GetOpetationData(_cacheUrl, cacheKey);
                string Username = string.Empty;
                string Password = string.Empty;
                bool doNotCall = false;
                bool addToState = true;

                if (operationalData != null)
                {
                    Username = operationalData.Username;
                    Password = operationalData.Password;

                }
                if (operationalData == null)
                    operationalData = new OperationDataModel();

                if (eventName == "btnexit")
                {
                    StateFactory<OperationDataModel>.RemoveStateFactory(_cacheUrl, cacheKey);
                    return DeviceCall(model.device.id);
                }
                if (eventName == "btnback")
                {
                    return BackButtonEvent(model, cacheKey);
                }
                if (eventName == "exception")
                {
                    if (model.state == "loginscreen")
                    {
                        return DeviceCall(model.device.id);
                    }
                    if (model.state == "menuscreen")
                    {
                        return BackExceptionEventWithSpecificScreenState(model, cacheKey, "GET");
                    }
                }
                if (eventName == "btnjumptowarehouse")
                {
                    return BackExceptionEventWithSpecificScreenState(model, cacheKey, "loginscreen");

                }
                if (eventName == "btnjumptolocations")
                {
                    return BackExceptionEventWithSpecificScreenState(model, cacheKey, "wrhsscreen");

                }
                Screen screen = new Screen();
                if (model.state == "loginscreen")
                {
                    doNotCall = false;
                    if (eventName == "submit")
                    {
                        using (GeneralExecution generalExecution = new GeneralExecution())
                        {
                            operationalData.Username = model.data.input[0].value;
                            operationalData.Password = model.data.input[1].value;
                            var loginStatus = generalExecution.DoLogin(new LoginModel { Username = operationalData.Username, Password = operationalData.Password });
                            if (loginStatus)
                            {
                                screen = DisplayFactory.InitializeScreen("wrhsscreen", "Menu");
                                var warehouseList = generalExecution.DoListWarehouses();
                                if (warehouseList != null)
                                {
                                    int i = 1;
                                    foreach (var item in warehouseList)
                                    {
                                        DisplayFactory.AddScreenElement(screen, DisplayFactory.CreateScreenElement(ScreenElementType.Button, "btnwarehouse_" + item, null, item, null, null, Font.S, Alignment.Center, null), i);
                                        i++;
                                    }
                                    DisplayFactory.AddScreenElement(screen, DisplayFactory.CreateScreenElement(ScreenElementType.Button, "btnexit", null, "Exit", null, Functionkey.FN2, Font.S, Alignment.Right, null), i + 1);
                                }
                                else
                                {
                                    throw new ScreenException(model.device.id, model.state, "Warehouse list is empty", "ERROR", ButtonTypes.exception, ExceptionTypes.Integration);
                                }
                            }
                            else
                            {
                                throw new ScreenException(model.device.id, model.state, "Wrong username and password", "ERROR", ButtonTypes.exception, ExceptionTypes.Integration);
                            }
                        }
                    }
                }
                if (model.state == "wrhsscreen")
                {
                    if (eventName.StartsWith("btnwarehouse_"))
                    {
                        operationalData.Warehouse = eventName.Replace("btnwarehouse_", "");
                        screen = DisplayFactory.InitializeScreen("localtionscreen", operationalData.Warehouse + " Locations");
                        DisplayFactory.AddScreenElement(screen, DisplayFactory.CreateScreenElement(ScreenElementType.Text, null, null, null, "From Location", null, Font.M, Alignment.Center, null), 1);
                        DisplayFactory.AddScreenElement(screen, DisplayFactory.CreateScreenElement(ScreenElementType.Input, "txtfromlocation", null, null, null, null, Font.M, Alignment.Center, null), 2);
                        DisplayFactory.AddScreenElement(screen, DisplayFactory.CreateScreenElement(ScreenElementType.Text, null, null, null, "To Location", null, Font.M, Alignment.Center, null), 3);
                        DisplayFactory.AddScreenElement(screen, DisplayFactory.CreateScreenElement(ScreenElementType.Input, "txttolocation", null, null, null, null, Font.M, Alignment.Center, null), 4);
                        DisplayFactory.AddScreenElement(screen, DisplayFactory.CreateScreenElement(ScreenElementType.Button, "btnback", null, "Back", null, Functionkey.FN1, Font.S, Alignment.Left, null), 8);
                        DisplayFactory.AddScreenElement(screen, DisplayFactory.CreateScreenElement(ScreenElementType.Button, "btnexit", null, "Exit", null, Functionkey.FN2, Font.S, Alignment.Right, null), 8);
                    }
                }
                if (model.state == "localtionscreen" || eventName == "btnreadmore")
                {
                    if (eventName != "btnreadmore")
                    {
                        if (model.data == null)
                        {
                            throw new ScreenException(model.device.id, model.state, "Locations are empty", "ERROR", ButtonTypes.exception, ExceptionTypes.Integration);
                        }
                        if (model.data.input.Count <= 0)
                        {
                            throw new ScreenException(model.device.id, model.state, "Locations are empty", "ERROR", ButtonTypes.exception, ExceptionTypes.Integration);
                        }
                        if (string.IsNullOrEmpty(model.data.input[0].value))
                        {
                            throw new ScreenException(model.device.id, model.state, "From Location is empty", "ERROR", ButtonTypes.exception, ExceptionTypes.Integration);
                        }
                        if (string.IsNullOrEmpty(model.data.input[1].value))
                        {
                            throw new ScreenException(model.device.id, model.state, "To Location is empty", "ERROR", ButtonTypes.exception, ExceptionTypes.Integration);
                        }
                        operationalData.FromLocation = model.data.input[0].value;
                        operationalData.ToLocation = model.data.input[1].value;
                    }
                    screen = DisplayFactory.InitializeScreen("itemscreen", "Item");
                    DisplayFactory.AddScreenElement(screen, DisplayFactory.CreateScreenElement(ScreenElementType.Text, null, null, null, "Item Code", null, Font.M, Alignment.Center, null), 1);
                    DisplayFactory.AddScreenElement(screen, DisplayFactory.CreateScreenElement(ScreenElementType.Input, "txtitemcode", null, null, null, null, Font.M, Alignment.Center, null), 2);
                    DisplayFactory.AddScreenElement(screen, DisplayFactory.CreateScreenElement(ScreenElementType.Text, null, null, null, "Quantity", null, Font.M, Alignment.Center, null), 3);
                    DisplayFactory.AddScreenElement(screen, DisplayFactory.CreateScreenElement(ScreenElementType.Input, "txtQuantity", null, null, null, null, Font.M, Alignment.Center, null), 4);
                    DisplayFactory.AddScreenElement(screen, DisplayFactory.CreateScreenElement(ScreenElementType.Button, "btnback", null, "Back", null, Functionkey.FN1, Font.S, Alignment.Left, null), 7);
                    DisplayFactory.AddScreenElement(screen, DisplayFactory.CreateScreenElement(ScreenElementType.Button, "btnexit", null, "Exit", null, Functionkey.FN2, Font.S, Alignment.Right, null), 7);
                    DisplayFactory.AddScreenElement(screen, DisplayFactory.CreateScreenElement(ScreenElementType.Button, "btnjumptolocations", null, "Loc.", null, Functionkey.FN3, Font.S, Alignment.Left, null), 8);
                    DisplayFactory.AddScreenElement(screen, DisplayFactory.CreateScreenElement(ScreenElementType.Button, "btnjumptowarehouse", null, "Wrhs", null, Functionkey.FN4, Font.S, Alignment.Right, null), 8);
                }
                if (model.state == "itemscreen")
                {
                    if (eventName == "submit")
                    {
                        addToState = false;
                        if (string.IsNullOrEmpty(model.data.input[0].value))
                        {
                            throw new ScreenException(model.device.id, model.state, "Item is empty", "ERROR", ButtonTypes.exception, ExceptionTypes.Integration);
                        }
                        if (string.IsNullOrEmpty(model.data.input[1].value))
                        {
                            throw new ScreenException(model.device.id, model.state, "Quantity is empty", "ERROR", ButtonTypes.exception, ExceptionTypes.Integration);
                        }
                        screen = DisplayFactory.InitializeScreen("itemreadscreen", "Message");
                        DisplayFactory.AddScreenElement(screen, DisplayFactory.CreateScreenElement(ScreenElementType.Text, null, null, null, "You readed ", null, Font.M, Alignment.Left, null), 1);
                        DisplayFactory.AddScreenElement(screen, DisplayFactory.CreateScreenElement(ScreenElementType.Text, null, null, null, "Item: " + model.data.input[0].value, null, Font.M, Alignment.Left, null), 2);
                        DisplayFactory.AddScreenElement(screen, DisplayFactory.CreateScreenElement(ScreenElementType.Text, null, null, null, "Qty: " + model.data.input[1].value, null, Font.M, Alignment.Left, null), 3);
                        DisplayFactory.AddScreenElement(screen, DisplayFactory.CreateScreenElement(ScreenElementType.Button, "btnreadmore", null, "Read More", null, Functionkey.FN1, Font.S, Alignment.Left, null), 5);
                        DisplayFactory.AddScreenElement(screen, DisplayFactory.CreateScreenElement(ScreenElementType.Button, "btnback", null, "Back", null, Functionkey.FN1, Font.S, Alignment.Left, null), 8);
                        DisplayFactory.AddScreenElement(screen, DisplayFactory.CreateScreenElement(ScreenElementType.Button, "btnexit", null, "Exit", null, Functionkey.FN2, Font.S, Alignment.Right, null), 8);

                    }
                }
                using (Display display = DisplayFactory.InitializeDisplay(DisplayMode.BarcodeScan, 0, screen, _indicator))
                {
                    StateFactory<OperationDataModel>.SetOpetationData(_cacheUrl, cacheKey, operationalData, DateTime.Now.AddMinutes(10));
                    if (addToState)
                    {
                        StateFactory<OperationDataModel>.AddOrUpdateStateMachineState(_cacheUrl, cacheKey, new StateModel { DoNotCall = doNotCall, Request = model, Response = display, ScreenId = model.state }, true, DateTime.Now.AddMinutes(10));
                    }
                    return display;
                }
            }
            catch (ScreenException ex)
            {
                string stateOfScreen = ex.State;
                if (string.IsNullOrEmpty(stateOfScreen))
                {
                    stateOfScreen = model.state;
                }
                return DisplayFactory.CreateException(stateOfScreen, ex.Header, ex.ErrorMessage, ButtonTypes.exception);
            }
        }
        private Display BackButtonEvent(DeviceRequest model, string cacheKey)
        {
            var backModel = StateFactory<OperationDataModel>.GetPreviousState(_cacheUrl, cacheKey);
            if (backModel != null)
            {
                if (!backModel.DoNotCall)
                {
                    return DeviceCall(backModel.Request);
                }
                else
                {
                    if (backModel != null && backModel.Response != null)
                    {
                        return backModel.Response;
                    }
                    else
                    {
                        return DeviceCall(model.device.id);
                    }
                }
            }
            else
            {
                return DeviceCall(model.device.id);
            }
        }

        private Display BackExceptionEventWithSpecificScreenState(DeviceRequest model, string cacheKey, string stateToJump)
        {
            var backModel = StateFactory<OperationDataModel>.GetStateWithName(_cacheUrl, cacheKey, stateToJump);
            if (backModel != null)
            {
                if (!backModel.DoNotCall)
                {
                    return DeviceCall(backModel.Request);
                }
                else
                {
                    if (backModel != null && backModel.Response != null)
                    {
                        return backModel.Response;
                    }
                    else
                    {
                        return DeviceCall(model.device.id);
                    }
                }
            }
            else
            {
                return DeviceCall(model.device.id);
            }
        }

        private static Indicator DoInitIndicator()
        {
            var buzzer = DisplayFactory.CreateBuzzer(Status.On, 100, 100, 1);
            var light = DisplayFactory.CreateLight(Color.Green, Status.On, 100, 100, 1);
            var vibration = DisplayFactory.CreateVibration(Status.On, 100, 100, 1);
            var indicator = DisplayFactory.InitializeIndicator(light, buzzer, vibration);
            return indicator;
        }
    }
}
