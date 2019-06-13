using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using Windows.Foundation;
using Windows.Foundation.Collections;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;
using Windows.UI.Xaml.Data;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Navigation;
using System.Threading.Tasks;
using Windows.System.Threading;
using Windows.Devices;
using Windows.Devices.Gpio;
using Windows.Devices.I2c;
using Windows.Devices.Enumeration;
using Microsoft.IoT.Lightning.Providers;
using Windows.Web.Http;
using System.Web;
using Windows.UI.Core;
using Windows.Web.Http;



// The Blank Page item template is documented at https://go.microsoft.com/fwlink/?LinkId=402352&clcid=0x409

namespace SmartWeatherStation
{
    /// <summary>
    /// An empty page that can be used on its own or navigated to within a Frame.
    /// </summary>
    public sealed partial class MainPage : Page
    {
        I2cDevice tempSensor=null;
        I2cDevice humiditySensor = null;

        ThreadPoolTimer speakWeather;
        DispatcherTimer uiTimer;
        GpioController gpioController;
       

        public MainPage()
        {
            this.InitializeComponent();

            if(LightningProvider.IsLightningEnabled)
            {
                LowLevelDevicesController.DefaultProvider = LightningProvider.GetAggregateProvider();
                
                System.Diagnostics.Debug.WriteLine("Lightning Success");
                
            }

           
            
            configSensors(); //Initialises the relevant sensors
            //Updates Readings Every 1/2 seconds.
            readPressure();
            readTemperature();
            

            //Initialising Dispatch Timer
            uiTimer = new DispatcherTimer();
            uiTimer.Interval = TimeSpan.FromMilliseconds(2000);
            uiTimer.Tick += UiTimer_Tick;
            Task.Delay(20).Wait();
            uiTimer.Start();
        }

        //==========================================================================
        //  Dweet.io 
        //==========================================================================

        HttpClient httpClient = new HttpClient();
        
        public async void sendDweet(double temp, double press, double humdty, double hsl,string forc)
        {
            string thingName = "MFCassim_RPi3";
            forc = forc.Replace(" ", "_");

            // Header Stuff
            var headers = httpClient.DefaultRequestHeaders;
            string header = "ie";
            if(!headers.UserAgent.TryParseAdd(header))
            {
                throw new Exception("Invalid header value: " + header);
            }

            header = "Mozilla/5.0 (compatible; MSIE 10.0; Windows NT 6.2; WOW64; Trident/6.0)";

            if(!headers.UserAgent.TryParseAdd(header))
            {
                throw new Exception("Invalid header value: " + header);
            }

            Uri dweetUri = new Uri("http://dweet.io/dweet/for/" + thingName + "?Temperature=" + temp + "&Pressure=" + press + "&Humidity=" + humdty + "&HeightAboveSeaLevel=" + hsl + "&WeatherForcast=" + forc);

            HttpResponseMessage httpResponse = new HttpResponseMessage();
            string httpResponceBody = "";
           
            try
            {
                httpResponse = await httpClient.GetAsync(dweetUri);
                httpResponse.EnsureSuccessStatusCode();
                httpResponceBody = await httpResponse.Content.ReadAsStringAsync();
                System.Diagnostics.Debug.Write(httpResponceBody);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.Write("Dweet Error:" + ex.HResult.ToString("X") + " Message: " + ex.Message);
            }
        }

        

        //==========================================================================
        //  Further Calculations 
        //==========================================================================

        double heightAboveSeaLevel ()
        {
            double temp_k = readTemperature() + 273.15;
            double pressure_sea_level = 1013.25; 
            double press_grad_h = Math.Pow((pressure_sea_level / readPressure()), 1 / 5.257);
            double press_grad_b = Math.Pow((readPressure() / pressure_sea_level), 1 / 5.255);
            double height_hyp = ((press_grad_h-1) * temp_k)*0.3048 / 0.0065;
            double height_bar = 44330*0.3048 * (1 - press_grad_b);
            height_bar = Math.Abs(height_bar);
            height_hyp = Math.Abs(height_hyp);
            System.Diagnostics.Debug.WriteLine("Hypsometric Height: " + height_hyp);
            System.Diagnostics.Debug.WriteLine("Barometric Height: " + height_bar);

            double height=height_bar;
            height = Math.Round(height, 2);
            return height;
        }

        string weatherForcast()
        {
            double cur_pressure = readPressure();

          if(press_list.Count>=3)
            {
                if(press_list[press_list.Count-1]>press_list[press_list.Count-2]) //Pressure Rising
                {
                    if(cur_pressure<=1030)
                    {
                        return "Settled Fine";
                    }
                    else if (cur_pressure<=1022)
                    {
                        return "Fine Weather";
                    }
                    else if (cur_pressure <= 1012)
                    {
                        return "Becoming Fine";
                    }
                    else if (cur_pressure <= 1007)
                    {
                        return "Fairly Fine, Improving";
                    }
                    else if (cur_pressure <= 1000)
                    {
                        return "Fairly Fine, Possibly showers, early";
                    }
                    else if (cur_pressure <= 995)
                    {
                        return "Showery Early, Improving";
                    }
                    else if (cur_pressure <= 990)
                    {
                        return "Changeable Mending";
                    }
                    else if (cur_pressure <= 984)
                    {
                        return "Rather Unsettled Clearing Later";
                    }
                    else if (cur_pressure <= 978)
                    {
                        return "Unsettled, Probably Improving";
                    }
                    else if (cur_pressure <= 970)
                    {
                        return "Unsettled, short fine Intervals";
                    }
                    else if (cur_pressure <= 965)
                    {
                        return "Very Unsettled, Finer at times;	";
                    }
                    else if (cur_pressure <= 959)
                    {
                        return "Stormy, possibly improving";
                    }
                    else
                    {
                        return "Stormy, much rain";
                    }


                }
                else if(press_list[press_list.Count - 1] == press_list[press_list.Count - 2]) //Pressure Stable
                {
                    if (cur_pressure <= 1033)
                    {
                        return "Settled Fine";
                    }
                    else if (cur_pressure <= 1023)
                    {
                        return "Fine Weather";
                    }
                    else if (cur_pressure <= 1014)
                    {
                        return "Fine, Possibly showers";
                    }
                    else if (cur_pressure <= 1008)
                    {
                        return "Fairly Fine , Showers likely";
                    }
                    else if (cur_pressure <= 1000)
                    {
                        return "Showery Bright Intervals";
                    }
                    else if (cur_pressure <= 994)
                    {
                        return "Changeable some rain";
                    }
                    else if (cur_pressure <= 989)
                    {
                        return "Unsettled, rain at times";
                    }
                    else if (cur_pressure <= 981)
                    {
                        return "Rain at Frequent Intervals";
                    }
                    else if (cur_pressure <= 974)
                    {
                        return "Very Unsettled, Rain";
                    }
                    else 
                    {
                        return "Stormy, much rain";
                    }
                    
                }
                else //Pressure Falling
                {

                    if (cur_pressure <= 1050)
                    {
                        return "Settled Fine";
                    }
                    else if (cur_pressure <= 1040)
                    {
                        return "Fine Weather";
                    }
                    else if (cur_pressure <= 1024)
                    {
                        return "Fine Becoming Less Settled";
                    }
                    else if (cur_pressure <= 1018)
                    {
                        return "Fairly Fine Showery Later";
                    }
                    else if (cur_pressure <= 1010)
                    {
                        return "Showery Becoming more unsettled";
                    }
                    else if (cur_pressure <= 1004)
                    {
                        return "Unsettled, Rain later";
                    }
                    else if (cur_pressure <= 998)
                    {
                        return "Rain at times, worse later";
                    }
                    else if (cur_pressure <= 991)
                    {
                        return "Rain at times, becoming very unsettled";
                    }
                    else 
                    {
                        return "Very Unsettled, Rain";
                    }
                    
                }
               
            }

            return "Awaiting Judgement";
        }
       
        //==========================================================================
        //  Data Lists
        //==========================================================================

        List<double> temp_list = new List<double>();
        List<double> press_list = new List<double>();
        List<double> humidity_list = new List<double>();

        //==========================================================================
        //  Timers and Loops
        //==========================================================================

        private void UiTimer_Tick(object sender, object e)
        {
            if(tempSensor!=null)
            {
                txtTemperature.Text = readTemperature().ToString();
                txtPressure.Text = readPressure().ToString();

                temp_list.Add(readTemperature());
                press_list.Add(readPressure());
                txtHeightAboveSea.Text = heightAboveSeaLevel().ToString();
                txtWeatherForcast.Text = weatherForcast();

                System.Diagnostics.Debug.WriteLine("Weather Forcast: " + weatherForcast());

                sendDweet(readTemperature(), readPressure(), readHumidity(), heightAboveSeaLevel(), weatherForcast());
            }

            if(humiditySensor!=null)
            {
                txtHumidity.Text = readHumidity().ToString();
                humidity_list.Add(readHumidity());
               
            }


        }

        //==========================================================================
        //  Config Sensors
        //==========================================================================

        public void configSensors()
        {
            
            I2C_init(); //CS Temperature Sensor
            
            I2C_write(tempSensor, 0x20, 0x00);  //Reset Tempsensor
            I2C_write(tempSensor, 0x20, 0xC4);  //PU,BDU and 25Hz
            I2C_write(tempSensor, 0x21, 0x00);  //Continous

            I2C_write(humiditySensor, 0x20, 0x00); //Reset
            I2C_write(humiditySensor, 0x20, 0x87); //PU,BDU and 12.5Hz
            I2C_write(humiditySensor, 0x21, 0x02); //Enable Heater
            Task.Delay(20).Wait();
            I2C_write(humiditySensor, 0x21, 0x00); //Disable Heater

        }

        //==========================================================================
        //  Sensor Methods
        //==========================================================================
        public double readTemperature()
        {
            // Read temp_l and temp_h registers
            byte temp_l = I2C_read(tempSensor, 0x20);
            byte temp_h = I2C_read(tempSensor, 0x2C);
            

            //Concatonate Temperature
            var temp = new byte[] { temp_l, temp_h };

            //Convert byte to double
            int i_temp = BitConverter.ToInt16(temp, 0);
            double temperature = 42.5 + (i_temp / 480);

            System.Diagnostics.Debug.WriteLine("Temperature: " + temperature.ToString());
            
            return temperature;
        }

        public double readPressure()
        {
            //Read press_xl, press_l and press_h registers
            byte press_xl = I2C_read(tempSensor, 0x28);
            byte press_l = I2C_read(tempSensor, 0x29);
            byte press_h = I2C_read(tempSensor, 0x2A);
            

            //Concatonate Pressure
            byte press_xh = 0x00; //Adding zero to front, to make BitConverter work
            var press = new byte[] { press_xl, press_l, press_h, press_xh };

            //Convert to double
            int i_press = BitConverter.ToInt32(press, 0);
            double pressure = (i_press / 4096)-7;

            System.Diagnostics.Debug.WriteLine("Pressure: " + pressure.ToString());

            return pressure;
        }

        public double readHumidity()
        {
            
            byte h0 = I2C_read(humiditySensor, 0x30);
            byte h1 = I2C_read(humiditySensor, 0x31);

            byte t0_l = I2C_read(humiditySensor, 0x36);
            byte t0_h = I2C_read(humiditySensor, 0x37);

            byte t1_l = I2C_read(humiditySensor, 0x3A);
            byte t1_h = I2C_read(humiditySensor, 0x3B);

            byte rawHum_l = I2C_read(humiditySensor, 0x28);
            byte rawHum_h = I2C_read(humiditySensor, 0x29);

            int i_h0 = Convert.ToInt16(h0); //H0 Value
            int i_h1 = Convert.ToInt16(h1); //H1 Value

            var t0 = new byte[] { t0_l, t0_h };
            int i_t0 = BitConverter.ToInt16(t0, 0); //T0 Value

            var t1 = new byte[] { t1_l, t1_h };
            int i_t1 = BitConverter.ToInt16(t1, 0); //T1 Value

            var rawHum = new byte[] { rawHum_l, rawHum_h };
            int i_rawHum = BitConverter.ToInt16(rawHum, 0); //HumidityOut Value 

            //Scaler
            double d_h0 = i_h0 / 2;
            double d_h1 = i_h1 / 2;

            //Calculation
            double gradient = (d_h1 - d_h0) / (i_t1 - i_t0);
            double humidity = gradient * (i_rawHum - i_t0) + d_h0;
            humidity = Math.Round(humidity, 2);
            System.Diagnostics.Debug.WriteLine("Humidity: " + humidity.ToString());
            return humidity;

        }


        //==========================================================================
        //  GPIO Methods
        //==========================================================================


        //==========================================================================
        //  I2C Methods
        //==========================================================================

        //Initialise connection to I2C Device
        public async void I2C_init ()
        {
     

            I2cController controller = await I2cController.GetDefaultAsync();
            tempSensor = controller.GetDevice(new I2cConnectionSettings(0x5C));
            humiditySensor = controller.GetDevice(new I2cConnectionSettings(0x5F));
        }

        //Read from I2C device: Request for variable is followed by recived data.
        public byte I2C_read(I2cDevice device,byte address)
        {
            var writeBuffer = new byte[] { address }; //Tx buffer for I2C
            var readBuffer = new byte[1]; //Rx buffer for I2C

            if(device!=null)
            {
                device.WriteReadPartial(writeBuffer, readBuffer);
                
               
            }

            return readBuffer.First();
        }

        //Write to I2C Device
        public void I2C_write (I2cDevice device, byte address,byte data)
        {
            var writeBuffer = new byte[] { address, data }; //Tx buffer for I2C

            if(device!=null)
            {
                device.WritePartial(writeBuffer);
                
            }
        }

        //==========================================================================
        //  UI Interactions
        //==========================================================================

        private void cmdCloud_Click(object sender, RoutedEventArgs e)
        {

        }

        private void cmdReset_Click(object sender, RoutedEventArgs e)
        {
            //Clears all Data Lists
            temp_list.Clear();
            press_list.Clear();
            humidity_list.Clear();
        }

        private void cmdStartAcq_Click(object sender, RoutedEventArgs e)
        {
            uiTimer.Start();
        }

        private void cmdStopAcq_Click(object sender, RoutedEventArgs e)
        {
            uiTimer.Stop();
        }
    }
}
