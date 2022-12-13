using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Diagnostics;
using System.Windows.Forms;
using System.Windows.Media;
using System.Text;
using System.ComponentModel;
using Microsoft.VisualBasic;

namespace WavetaleTrainer
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {

        globalKeyboardHook kbHook = new globalKeyboardHook();
        Timer updateTimer;
        Process game;
        public bool hooked = false;

        DeepPointer persistentDP, writeDP, cheatDataDP, glidingDataDP;
        IntPtr xPosPtr, yPosPtr, zPosPtr, xWritePtr, yWritePtr, zWritePtr, xzVelPtr, yVelPtr, invisiblePtr, invulnerablePtr, glidingGravityPtr, glidingSpeedPtr;
        float xPos, yPos, zPos, xzVel, yVel, avgXZVel, glidingGravity, glidingSpeed;
        bool invisible, invulnerable;

        List<float> avgVelList = new List<float>();

        float[] savedPos = new float[3] { 0f, 0f, 0f };

        public MainWindow()
        {
            InitializeComponent();

            kbHook.KeyDown += InputKeyDown;
            kbHook.KeyUp += InputKeyUp;
            kbHook.HookedKeys.Add(System.Windows.Forms.Keys.F5);
            kbHook.HookedKeys.Add(System.Windows.Forms.Keys.F6);
            kbHook.HookedKeys.Add(System.Windows.Forms.Keys.F7);
            kbHook.HookedKeys.Add(System.Windows.Forms.Keys.F8);
            kbHook.HookedKeys.Add(System.Windows.Forms.Keys.F9);


            updateTimer = new Timer
            {
                Interval = (16) // ~60 Hz
            };
            updateTimer.Tick += new EventHandler(Update);
            updateTimer.Start();
        }

        private void Update(object sender, EventArgs e)
        {
            if (game == null || game.HasExited)
            {
                game = null;
                hooked = false;
            }
            if (!hooked)
                hooked = Hook();
            if (!hooked)
                return;
            try
            {
                DerefPointers();
            }
            catch (Exception)
            {
                return;
            }

            game.ReadValue<float>(xPosPtr, out xPos);
            game.ReadValue<float>(yPosPtr, out yPos);
            game.ReadValue<float>(zPosPtr, out zPos);

            game.ReadValue<float>(xzVelPtr, out xzVel);
            game.ReadValue<float>(yVelPtr, out yVel);

            game.ReadValue<bool>(invisiblePtr, out invisible);
            game.ReadValue<bool>(invulnerablePtr, out invulnerable);
            game.ReadValue<float>(glidingGravityPtr, out glidingGravity);
            game.ReadValue<float>(glidingSpeedPtr, out glidingSpeed);

            if (avgVelList.Count >= 600) // This is roughly 10 seconds?
                avgVelList.RemoveAt(0);
                

            avgVelList.Add(xzVel);

            avgXZVel = avgVelList.Average();

            positionBlock.Text = xPos.ToString("0.00") + "\n" + yPos.ToString("0.00") + "\n" + zPos.ToString("0.00");
            horiSpeedBlock.Text = xzVel.ToString("0.00");
            verSpeedBlock.Text = yVel.ToString("0.00");
            avgBlock.Text = avgXZVel.ToString("0.00");
        }

        private void SavePosBtn_Click(object sender, RoutedEventArgs e)
        {
            SavePosition();
        }

        private void LoadPosBtn_Click(object sender, RoutedEventArgs e)
        {
            LoadPosition();
        }

        private void InvulnerableBtn_Click(object sender, RoutedEventArgs e)
        {
            Invulnerable();
        }

        private void InvisibleBtn_Click(object sender, RoutedEventArgs e)
        {
            Invisible();
        }

        private void FlyBtn_Click(object sender, RoutedEventArgs e)
        {
            Fly();
        }

        private void Fly()
        {
            if (glidingGravity == -13f && glidingSpeed == 10000f)
            {
                game.WriteBytes(glidingGravityPtr, BitConverter.GetBytes(13f));
                game.WriteBytes(glidingSpeedPtr, BitConverter.GetBytes(10f));
            }
            else
            { 
                game.WriteBytes(glidingGravityPtr, BitConverter.GetBytes(-13f));
                game.WriteBytes(glidingSpeedPtr, BitConverter.GetBytes(10000f));
            }
        }

        private void Invisible()
        {
            game.WriteBytes(invisiblePtr, BitConverter.GetBytes(!invisible));
        }

        private void Invulnerable()
        {
            game.WriteBytes(invulnerablePtr, BitConverter.GetBytes(!invulnerable));
        }

        private void SavePosition()
        {
            savedPos = new float[3] { xPos, yPos, zPos };
        }

        private void LoadPosition()
        {
            //IncInj();
            game.WriteBytes(xWritePtr, BitConverter.GetBytes(savedPos[0]));
            game.WriteBytes(yWritePtr, BitConverter.GetBytes(savedPos[1]));
            game.WriteBytes(zWritePtr, BitConverter.GetBytes(savedPos[2]));
        }

        private void InputKeyDown(object sender, KeyEventArgs e)
        {
            switch (e.KeyCode)
            {
                case Keys.F5:
                    SavePosition();
                    break;
                case Keys.F6:
                    LoadPosition();
                    break;
                case Keys.F7:
                    Invulnerable();
                    break;
                case Keys.F8:
                    Invisible();
                    break;
                case Keys.F9:
                    Fly();
                    break;
                default:
                    break;
            }
            e.Handled = true;
        }

        private void InputKeyUp(object sender, KeyEventArgs e)
        {
            e.Handled = true;
        }

        private bool Hook()
        {
            List<Process> processList = Process.GetProcesses().ToList().FindAll(x => x.ProcessName.Contains("Wavetale"));
            if (processList.Count == 0)
            {
                game = null;
                return false;
            }
            game = processList[0];

            if (game.HasExited)
                return false;

            try
            {
                int mainModuleSize = game.MainModule.ModuleMemorySize;
                SetPointersByModuleSize(mainModuleSize);
                return true;
            }
            catch (Win32Exception ex)
            {
                Console.WriteLine(ex.ErrorCode);
                return false;
            }
        }

        private void SetPointersByModuleSize(int moduleSize)
        {
            switch (moduleSize)
            {

                case 667648:
                    Debug.WriteLine("Found version 1.00 (" + moduleSize + ")");
                    persistentDP = new DeepPointer("GameAssembly.dll", 0x027FEAC0, 0xB8, 0x30, 0x78, 0x18, 0x0);
                    cheatDataDP = new DeepPointer("GameAssembly.dll", 0x027FEAC0, 0xB8, 0x30, 0x78, 0x18, 0x178, 0x0);
                    glidingDataDP = new DeepPointer("GameAssembly.dll", 0x027FEAC0, 0xB8, 0x30, 0x58, 0x98, 0x0);
                    writeDP = new DeepPointer("UnityPlayer.dll", 0x018476E8, 0x58, 0x140, 0x210, 0x90);
                    //positionDP = new DeepPointer(0x02669A50, 0x1E8, 0xA0, 0x120);
                    break;
                default:
                    updateTimer.Stop();
                    Console.WriteLine(moduleSize.ToString());
                    System.Windows.Forms.MessageBox.Show("This game version (" + moduleSize.ToString() + ") is not supported.", "Unsupported Game Version");
                    Environment.Exit(0);
                    break;
            }
        }

        private void DerefPointers()
        {
            IntPtr persistentPtr;
            persistentDP.DerefOffsets(game, out persistentPtr);
            xPosPtr = persistentPtr + 0x10;
            yPosPtr = persistentPtr + 0x14;
            zPosPtr = persistentPtr + 0x18;
            xzVelPtr = persistentPtr + 0x24;
            yVelPtr = persistentPtr + 0x28;

            IntPtr cheatDataPtr;
            cheatDataDP.DerefOffsets(game, out cheatDataPtr);
            invisiblePtr = cheatDataPtr + 0x10;
            invulnerablePtr = cheatDataPtr + 0x11;

            IntPtr glidingDataPtr;
            glidingDataDP.DerefOffsets(game, out glidingDataPtr);
            glidingGravityPtr = glidingDataPtr + 0x14;
            glidingSpeedPtr = glidingDataPtr + 0x98;

            IntPtr writePtr;
            writeDP.DerefOffsets(game, out writePtr);
            xWritePtr = writePtr;
            yWritePtr = writePtr + 0x4;
            zWritePtr = writePtr + 0x8;
            /*xPosDP.DerefOffsets(game, out xPosPtr);
            yPosDP.DerefOffsets(game, out yPosPtr);
            zPosDP.DerefOffsets(game, out zPosPtr);*/
        }
    }
}
