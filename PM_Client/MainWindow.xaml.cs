using System;
using System.Net.Sockets;
using System.Net;
using System.Runtime.Serialization.Formatters.Binary;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Forms;
using System.Windows.Input;
using System.Windows.Media;
using System.Threading;
using System.Drawing;
using System.Windows.Shapes;
using System.Collections.Generic;
using System.Runtime.Serialization;

namespace PM_Client
{
    public class Client
    {
        public byte id;
        public SolidColorBrush brush;
        public int thickness;
        public double x;
        public double y;
    }

    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        //Connection fields
        private Socket socket;
        private IPAddress serverAddress;
        private int serverPort;
        private EndPoint serverEndPoint;
        private bool isConnected = false;

        //Drawing fields
        private int thickness;
        private SolidColorBrush brush = new SolidColorBrush(Colors.Black);
        bool isDrawing = false;
        byte[] buffer = new byte[1024];
        private Thread receiveDataThread;
        private List<Client> listOfClients;

        public MainWindow()
        {
            InitializeComponent();

            listOfClients = new List<Client>();
            thickness = (int)slider.Value;
            RefreshControls();
        }

        private void Window_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            if (isConnected)
                button_Disconnect.RaiseEvent(new RoutedEventArgs(System.Windows.Controls.Button.ClickEvent));
        }

        private void RefreshControls()
        {
            //Connection
            textBox_IP.IsEnabled = isConnected ? false : true;
            textBox_Port.IsEnabled = isConnected ? false : true;
            button_Connect.IsEnabled = isConnected ? false : true;
            button_Disconnect.IsEnabled = isConnected ? true : false;
            textBox_Connection.Text = isConnected ? "Connected" : "Disconnected";

            //Brush
            textBox_Thickness.Text = thickness.ToString();
            rectangle.Fill = brush;
        }

        private async void Button_Connect_Click(object sender, RoutedEventArgs e)
        {
            //Localhost check
            if (textBox_IP.Text == "localhost")
                textBox_IP.Text = "127.0.0.1";

            //Parse
            if (!IPAddress.TryParse(textBox_IP.Text, out serverAddress) || !Int32.TryParse(textBox_Port.Text, out serverPort))
            {
                System.Windows.MessageBox.Show("Podaj poprawne dane", "Okno", MessageBoxButton.OK);
                return;
            }

            try
            {
                //Connect
                socket = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);
                serverEndPoint = new IPEndPoint(serverAddress, serverPort);

                //Send connect signal
                byte code = 0x00;
                string message = "connect";
                BinaryFormatter formatter = new BinaryFormatter();
                byte[] data = null;
                using (var ms = new System.IO.MemoryStream())
                {
                    formatter.Serialize(ms, code);
                    formatter.Serialize(ms, message);
                    data = ms.ToArray();
                }
                socket.SendTo(data, serverEndPoint);

                isConnected = true;
                RefreshControls();

                //Run listening task
                //Task.Run(ReceiveData);
                receiveDataThread = new Thread(new ThreadStart(ReceiveData));
                receiveDataThread.SetApartmentState(ApartmentState.STA);
                receiveDataThread.Start();
                //thread.Join();
            }
            catch (Exception ex)
            {
                System.Windows.MessageBox.Show($"Błąd podczas połączenia z serwerem: {ex.Message}", "Błąd", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private async void ReceiveData()
        {
            try
            {
                while (true)
                {
                    int receivedBytes = socket.ReceiveFrom(buffer, ref serverEndPoint);
                    HandleReceivedData(receivedBytes);
                }
            }
            catch (SocketException)
            {
                //SocketException when socket closed
            }
            catch (Exception ex)
            {
                System.Windows.MessageBox.Show($"Błąd podczas odbierania danych: {ex.Message}", "Błąd", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private async void Button_Disconnect_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                //Send disconnect signal
                byte code = 0x00;
                string message = "disconnect";
                BinaryFormatter formatter = new BinaryFormatter();
                byte[] data = null;
                using (var ms = new System.IO.MemoryStream())
                {
                    formatter.Serialize(ms, code);
                    formatter.Serialize(ms, message);
                    data = ms.ToArray();
                }
                socket.SendTo(data, serverEndPoint);

                socket.Close();
                isConnected = false;
                RefreshControls();
            }
            catch (Exception ex)
            {
                System.Windows.MessageBox.Show($"Błąd podczas zamykania połączenia: {ex.Message}", "Błąd", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void HandleReceivedData(int receivedBytes)
        {
            BinaryFormatter formatter = new BinaryFormatter();
            using (var ms = new System.IO.MemoryStream(buffer, 0, receivedBytes))
            {
                byte code = (byte)formatter.Deserialize(ms);

                if (code == 0x00)
                {
                    //Receive port from server
                    int port = (int)formatter.Deserialize(ms);
                }
                else if (code == 0x01)
                {
                    //Receive brush, point and id
                    byte a = (byte)formatter.Deserialize(ms);
                    byte r = (byte)formatter.Deserialize(ms);
                    byte g = (byte)formatter.Deserialize(ms);
                    byte b = (byte)formatter.Deserialize(ms);
                    int thickness = (int)formatter.Deserialize(ms);

                    double x = (double)formatter.Deserialize(ms);
                    double y = (double)formatter.Deserialize(ms);

                    byte id = (byte)formatter.Deserialize(ms);

                    StartDrawing(a, r, g, b, thickness, x, y, id);
                }
                else if (code == 0x02)
                {
                    //Receive point and id
                    double x = (double)formatter.Deserialize(ms);
                    double y = (double)formatter.Deserialize(ms);

                    byte id = (byte)formatter.Deserialize(ms);

                    KeepDrawing(x, y, id);
                }
                else if (code == 0x03)
                {
                    //Receive id
                    byte id = (byte)formatter.Deserialize(ms);

                    EndDrawing(id);
                }
            }
        }

        private void StartDrawing(byte a, byte r, byte g, byte b, int thickness, double x, double y, byte id)
        {
            Dispatcher.Invoke(() =>
            {
                //Create brush
                System.Windows.Media.Color newColor = System.Windows.Media.Color.FromArgb(a, r, g, b);
                SolidColorBrush newBrush = new SolidColorBrush(newColor);

                //Create client
                Client client = new Client();
                client.id = id;
                client.x = x;
                client.y = y;
                client.brush = newBrush;
                client.thickness = thickness;

                //Add client to the list
                listOfClients.Add(client);
            });
        }

        private void KeepDrawing(double x, double y, byte id)
        {
            Dispatcher.Invoke(() =>
            {
                //Get client
                Client client = listOfClients.Find(x => x.id == id);
                if (client == null)
                    return;

                //Create and draw the line
                Line line = new Line()
                {
                    X1 = client.x,
                    Y1 = client.y,
                    X2 = x,
                    Y2 = y,
                    Stroke = client.brush,
                    StrokeThickness = client.thickness
                };
                canvas.Children.Add(line);

                //Update client point
                client.x = x;
                client.y = y;
            });
        }

        private void EndDrawing(byte id)
        {
            Dispatcher.Invoke(() =>
            {
                //Get client
                Client client = listOfClients.Find(x => x.id == id);

                if (client != null)
                {
                    //Remove client from the list
                    listOfClients.Remove(client);
                }
            });
        }

        private void canvas_MouseDown(object sender, MouseButtonEventArgs e)
        {
            if (!isConnected)
                return;

            //Send color & first cooridnates
            byte code = 0x01;

            byte a = brush.Color.A;
            byte r = brush.Color.R;
            byte g = brush.Color.G;
            byte b = brush.Color.B;

            double x = e.GetPosition(this).X;
            double y = e.GetPosition(this).Y;

            BinaryFormatter formatter = new BinaryFormatter();
            byte[] data = null;
            using (var ms = new System.IO.MemoryStream())
            {
                formatter.Serialize(ms, code);

                formatter.Serialize(ms, a);
                formatter.Serialize(ms, r);
                formatter.Serialize(ms, g);
                formatter.Serialize(ms, b);
                formatter.Serialize(ms, thickness);

                formatter.Serialize(ms, x);
                formatter.Serialize(ms, y);

                data = ms.ToArray();
            }
            socket.SendTo(data, serverEndPoint);

            isDrawing = true;
        }

        private void canvas_MouseMove(object sender, System.Windows.Input.MouseEventArgs e)
        {
            if (!(isConnected && isDrawing))
                return;

            //Send cooridnates
            byte code = 0x02;

            double x = e.GetPosition(this).X;
            double y = e.GetPosition(this).Y;

            BinaryFormatter formatter = new BinaryFormatter();
            byte[] data = null;
            using (var ms = new System.IO.MemoryStream())
            {
                formatter.Serialize(ms, code);

                formatter.Serialize(ms, x);
                formatter.Serialize(ms, y);

                data = ms.ToArray();
            }
            socket.SendTo(data, serverEndPoint);
        }

        private void canvas_MouseUp(object sender, MouseButtonEventArgs e)
        {
            if (!isConnected)
                return;

            //Send stop signal
            byte code = 0x03;

            BinaryFormatter formatter = new BinaryFormatter();
            byte[] data = null;
            using (var ms = new System.IO.MemoryStream())
            {
                formatter.Serialize(ms, code);

                data = ms.ToArray();
            }
            socket.SendTo(data, serverEndPoint);

            isDrawing = false;
        }

        private void canvas_MouseEnter(object sender, System.Windows.Input.MouseEventArgs e)
        {
            if (isConnected)
                Mouse.OverrideCursor = System.Windows.Input.Cursors.Pen;
        }

        private void canvas_MouseLeave(object sender, System.Windows.Input.MouseEventArgs e)
        {
            Mouse.OverrideCursor = null;
        }

        private void rectangle_MouseDown(object sender, MouseButtonEventArgs e)
        {
            var dialog = new ColorDialog();
            DialogResult result = dialog.ShowDialog();

            if (result == System.Windows.Forms.DialogResult.OK)
            {
                System.Drawing.Color color = dialog.Color;
                System.Windows.Media.Color newColor = System.Windows.Media.Color.FromArgb(color.A, color.R, color.G, color.B);
                brush = new SolidColorBrush(newColor);
                rectangle.Fill = brush;
            }
        }

        private void Slider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            thickness = (int)slider.Value;

            if (textBox_Thickness != null)
                textBox_Thickness.Text = thickness.ToString();
        }

        private void textBox_Thickness_TextChanged(object sender, TextChangedEventArgs e)
        {
            int value;

            //Parse
            if (!Int32.TryParse(textBox_Thickness.Text, out value))
            {
                textBox_Thickness.Text = thickness.ToString();
                return;
            }

            //Check
            if (value > 10)
                value = 10;
            if (value < 1)
                value = 1;

            //Set
            slider.Value = (double)value;
        }
    }
}