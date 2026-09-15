// ------------------------------------------------
// This console program demonstrates how to control a CiA-402 motor using TwinCAT ADS without
// Visual Studio or TwinCAT XAE.
// Process overview:
// 1. Connect to the local TwinCAT PLC Runtime 1 (ADS Port 851).
// 2. Read the current PLC state and start the PLC if it is not running.
// 3. Reset TwinSAFE errors by pulsing the MAIN.bERRAckIn variable.
// 4. Reset the CiA-402 motor fault by writing 128 to the MAIN.wControlword variable.
// 5. Start the motor by following the CiA-402 state machine sequence (6 -> 7 -> 15) and setting the target velocity to 333.
// 6. The program includes user prompts for manual actions, such as pressing buttons on the RZ/T2 board and handling emergency stops.
// 
//  Requirements on Target PC: 
//  - TwinCAT 3 Runtime installed and running
//  - TwinCAT PLC project deployed to the target PC
//  - Modified Main program in the PLC project to include wControlword and diTargetVelocity variables for motor control
//  - Link motor controlword and target velocity to wControlword and diTargetVelocity in the PLC project
//
//  Notes:
//  - This program uses the TwinCAT.Ads NuGet package for ADS communication.
//  - The program is designed for presentation purposes and will require modifications for specific hardware setups or safety requirements.
//
//  Version History:
//  8/25/2026 CL
//  - Initial version for TwinCAT Motor Controller demonstration
// ------------------------------------------------

using System;
using TwinCAT.Ads;

namespace TwinCATMotorController
{
    internal class Program
    {
        // ------------------------------------------------
        // USER SETTINGS
        // ------------------------------------------------

        // PLC Runtime 1
        private const int PlcAdsPort = 851;

        // TwinSAFE error acknowledge input
        private const string ErrorAckVariable = "MAIN.bERRAckIn";

        // CiA-402 motor variables
        private const string MotorControlwordVariable = "MAIN.wControlword";

        private const string MotorVelocityVariable = "MAIN.diTargetVelocity";

        // ------------------------------------------------
        // TIMING SETTINGS
        // ------------------------------------------------

        private const int ErrorAckLowTimeMs = 50;

        private const int ErrorAckPulseMs = 100;

        private const int AfterTwinSafeResetMs = 200;

        private const int MotorFaultResetPulseMs = 100;

        private const int AfterStartRunMs = 1000;


        // ------------------------------------------------
        // MAIN
        // ------------------------------------------------

        static void Main(string[] args)
        {
            try
            {
                Console.WriteLine("Starting TwinCAT Motor Controller Log");
                Console.WriteLine();

                // ----------------------------------------------------
                // CONNECT TO LOCAL TWINCAT PLC
                //
                // ADS Port 851 = PLC Runtime 1
                //
                // No Visual Studio
                // No TwinCAT XAE
                // No EnvDTE
                // No TCatSysManagerLib
                // ----------------------------------------------------

                using (AdsClient ads = new AdsClient())
                {
                    Console.WriteLine("Connecting to PLC Runtime 1...");

                    ads.Connect(PlcAdsPort);

                    Console.WriteLine("Connected to ADS Port 851.");

                    // ------------------------------------------------
                    // READ PLC STATE
                    // ------------------------------------------------

                    StateInfo state = ads.ReadState();

                    Console.WriteLine();
                    Console.WriteLine("Current ADS State: " + state.AdsState);
                    Console.WriteLine("Current Device State: " + state.DeviceState);

                    // ------------------------------------------------
                    // START PLC IF NECESSARY
                    // ------------------------------------------------

                    if (state.AdsState != AdsState.Run)
                    {
                        Console.WriteLine();
                        Console.WriteLine("PLC is not RUN.");
                        Console.WriteLine("Sending PLC RUN command...");

                        ads.WriteControl(new StateInfo(AdsState.Run, state.DeviceState));

                        System.Threading.Thread.Sleep(AfterStartRunMs);

                        state = ads.ReadState();

                        Console.WriteLine("New ADS State: " + state.AdsState);
                    }

                    // ------------------------------------------------
                    // VERIFY PLC IS RUNNING
                    // ------------------------------------------------

                    if (state.AdsState != AdsState.Run)
                    {
                        throw new Exception("PLC Runtime failed to enter RUN state.\n\nMake sure the TwinCAT PLC project has already been deployed to this PC.");
                    }

                    Console.WriteLine();
                    Console.WriteLine("PLC Runtime is RUNNING.");

                    // ------------------------------------------------
                    //
                    // TWINSAFE ERROR ACKNOWLEDGE
                    //
                    // MAIN.bERRAckIn
                    //
                    // FALSE -> TRUE -> FALSE
                    // ------------------------------------------------

                    Console.WriteLine();
                    Console.WriteLine("Resetting TwinSAFE...");

                    PulseTwinSafeErrorAck(ads);

                    Console.WriteLine();
                    Console.WriteLine("TwinSAFE acknowledge completed.");

                    // ------------------------------------------------
                    //
                    // RESET CiA-402 MOTOR FAULT
                    //
                    // Controlword:
                    //
                    // 128 -> 0
                    // ------------------------------------------------

                    ResetMotorFault(ads);

                    Console.WriteLine();
                    Console.WriteLine("Motor fault reset sequence completed.");

                    System.Threading.Thread.Sleep(AfterTwinSafeResetMs);


                    // ====================================================
                    // USER ACTION
                    //
                    //Push button 8 and 10 on the RZ/T2 board to prepare for motor operation.
                    // ====================================================

                    Console.WriteLine();
                    Console.WriteLine("===============USER ACTION===========================");
                    Console.WriteLine("Press Button 8 and 10 on the RZ/T2 board to prepare for motor operation.");
                    Console.WriteLine("When ready to continue, Press ENTER to start the motor.");
                    Console.WriteLine("=====================================================");

                    Console.ReadLine();

                    // ------------------------------------------------
                    //
                    // START MOTOR
                    //
                    // Controlword:
                    //
                    // 6 -> 7 -> 15
                    //
                    // Target Velocity:
                    //
                    // 333
                    // ------------------------------------------------

                    StartMotor(ads);

                    // ------------------------------------------------
                    // MOTOR STARTED
                    // ------------------------------------------------

                    Console.WriteLine();
                    Console.WriteLine(
                        "Motor start command completed.");

                    // ====================================================
                    // USER ACTION
                    //
                    // EMERGENCY STOP
                    // ====================================================

                    Console.WriteLine();
                    Console.WriteLine("===============USER ACTION===========================");
                    Console.WriteLine("Press Emergency Stop button to stop the motor.");
                    Console.WriteLine("Press ENTER after motor is stopped.");
                    Console.WriteLine("=====================================================");

                    Console.ReadLine();

                    // ====================================================
                    // USER ACTION
                    //
                    // USER RELEASES EMERGENCY STOP
                    // ====================================================

                    Console.WriteLine();
                    Console.WriteLine("===============USER ACTION===========================");
                    Console.WriteLine("Release Emergency Stop by twisting the red button clockwise.");
                    Console.WriteLine("Press Button 8 and Button 10 on the RZ/T2 board.");
                    Console.WriteLine("When ready, Press ENTER to continue.");
                    Console.WriteLine("=====================================================");

                    Console.ReadLine();

                    // ------------------------------------------------
                    // RESET MOTOR AGAIN
                    // ------------------------------------------------

                    ResetMotorFault(ads);

                    System.Threading.Thread.Sleep(AfterTwinSafeResetMs);

                    // ------------------------------------------------
                    // START MOTOR AGAIN
                    // ------------------------------------------------

                    StartMotor(ads);

                    Console.WriteLine();
                    Console.WriteLine("Motor start sequence completed.");
                    Console.WriteLine();
                    Console.WriteLine("------------------------------------------------");
                    Console.WriteLine("This is the end of the program. Press ENTER to EXIT.");
                    Console.WriteLine("------------------------------------------------");

                    Console.ReadLine();
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine();
                Console.WriteLine("!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!");
                Console.WriteLine("ERROR");
                Console.WriteLine("!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!");
                Console.WriteLine();
                Console.WriteLine(ex.ToString());
                Console.WriteLine();
                Console.WriteLine("Press ENTER to exit.");

                Console.ReadLine();
            }
        }

        // ----------------------------------------------------
        // TWINSAFE ERROR ACKNOWLEDGE
        //
        // MAIN.bERRAckIn
        //
        // FALSE -> TRUE -> FALSE
        // ----------------------------------------------------

        private static void PulseTwinSafeErrorAck(AdsClient ads)
        {
            Console.WriteLine();
            Console.WriteLine("Creating ADS handle for:");
            Console.WriteLine(ErrorAckVariable);

            uint errorAckHandle = 0;

            try
            {
                // Create ADS variable handle

                errorAckHandle = (uint)ads.CreateVariableHandle(ErrorAckVariable);

                // ----------------------------------------------------
                // Set signal LOW to prepare for TwinSAFE reset
                // ----------------------------------------------------

                Console.WriteLine();
                Console.WriteLine("TwinSAFE Error ACK = 0");

                ads.WriteAny(errorAckHandle, false);

                System.Threading.Thread.Sleep(ErrorAckLowTimeMs);

                // ----------------------------------------------------
                // Set signal HIGH to reset TwinSAFE error
                // ----------------------------------------------------

                Console.WriteLine("TwinSAFE Error ACK = 1");

                ads.WriteAny(errorAckHandle, true);

                System.Threading.Thread.Sleep(ErrorAckPulseMs);

                // ----------------------------------------------------
                // Set signal LOW to proceed with TwinSAFE reset
                // ----------------------------------------------------

                Console.WriteLine("TwinSAFE Error ACK = 0");

                ads.WriteAny(errorAckHandle, false);

                Console.WriteLine();
                Console.WriteLine("TwinSAFE reset pulse finished.");
            }
            finally
            {
                if (errorAckHandle != 0)
                {
                    ads.DeleteVariableHandle(errorAckHandle);
                }
            }
        }

        // ----------------------------------------------------
        // RESET MOTOR FAULT
        //
        // CiA-402
        //
        // Controlword = 128 resets the motor fault
        //
        // Then:
        //
        // Controlword = 0 stops the motor
        // ----------------------------------------------------

        private static void ResetMotorFault(AdsClient ads)
        {
            Console.WriteLine();
            Console.WriteLine("Resetting CiA-402 motor fault...");
            Console.WriteLine("Variable: " + MotorControlwordVariable);

            uint controlwordHandle = 0;

            try
            {
                controlwordHandle = (uint)ads.CreateVariableHandle(MotorControlwordVariable);

                // ------------------------------------------------
                // FAULT RESET
                //
                // 128 = 0x0080
                // ------------------------------------------------

                Console.WriteLine();
                Console.WriteLine("Controlword = 128 (Fault Reset)");

                ads.WriteAny(controlwordHandle, (ushort)128);

                System.Threading.Thread.Sleep(MotorFaultResetPulseMs);


                // ------------------------------------------------
                // RETURN TO ZERO
                // ------------------------------------------------

                Console.WriteLine("Controlword = 0");

                ads.WriteAny(controlwordHandle, (ushort)0);

                Console.WriteLine("Motor fault reset pulse finished.");
            }
            finally
            {
                if (controlwordHandle != 0)
                {
                    ads.DeleteVariableHandle(controlwordHandle);
                }
            }
        }

        // ------------------------------------------------
        // START MOTOR
        //
        // CiA-402 sequence:
        //
        // 6 -> 7 -> 15
        //
        // Target Velocity = 333
        // ------------------------------------------------

        private static void StartMotor(AdsClient ads)
        {
            Console.WriteLine();
            Console.WriteLine("Starting motor...");

            uint controlwordHandle = 0;
            uint velocityHandle = 0;

            try
            {
                // ------------------------------------------------
                // CREATE CONTROLWORD HANDLE
                // ------------------------------------------------

                Console.WriteLine("Creating Controlword handle:");
                Console.WriteLine(MotorControlwordVariable);

                controlwordHandle = (uint)ads.CreateVariableHandle(MotorControlwordVariable);

                // ------------------------------------------------
                // CREATE VELOCITY HANDLE
                // ------------------------------------------------

                Console.WriteLine();
                Console.WriteLine("Creating Target Velocity handle:");
                Console.WriteLine(MotorVelocityVariable);

                velocityHandle = (uint)ads.CreateVariableHandle(MotorVelocityVariable);

                // ------------------------------------------------
                // SHUTDOWN
                //
                // Controlword = 6
                // ------------------------------------------------

                Console.WriteLine();
                Console.WriteLine("Controlword = 6");

                ads.WriteAny(controlwordHandle, (ushort)6);

                System.Threading.Thread.Sleep(AfterTwinSafeResetMs);

                // ------------------------------------------------
                // SWITCH ON
                //
                // Controlword = 7
                // ------------------------------------------------

                Console.WriteLine("Controlword = 7");

                ads.WriteAny(controlwordHandle, (ushort)7);

                System.Threading.Thread.Sleep(AfterTwinSafeResetMs);

                // ------------------------------------------------
                // ENABLE OPERATION
                //
                // Controlword = 15
                // ------------------------------------------------

                Console.WriteLine("Controlword = 15");

                ads.WriteAny(controlwordHandle, (ushort)15);

                System.Threading.Thread.Sleep(AfterTwinSafeResetMs);

                // ------------------------------------------------
                // READ BACK CONTROLWORD
                // ------------------------------------------------

                ushort controlwordReadback = (ushort)ads.ReadAny(controlwordHandle, typeof(ushort));

                Console.WriteLine("Controlword readback = " + controlwordReadback);

                // ------------------------------------------------
                // TARGET VELOCITY
                //
                // Velocity = 333
                // ------------------------------------------------

                Console.WriteLine();
                Console.WriteLine("Target Velocity = 333");

                ads.WriteAny(velocityHandle, 333);

                System.Threading.Thread.Sleep(AfterTwinSafeResetMs);

                // ------------------------------------------------
                // READ BACK VELOCITY
                // ------------------------------------------------

                int velocityReadback = (int)ads.ReadAny(velocityHandle, typeof(int));

                Console.WriteLine("Velocity readback = " + velocityReadback);
                Console.WriteLine();
                Console.WriteLine("Motor start command completed.");
            }
            finally
            {
                if (velocityHandle != 0)
                {
                    ads.DeleteVariableHandle(velocityHandle);
                }

                if (controlwordHandle != 0)
                {
                    ads.DeleteVariableHandle(controlwordHandle);
                }
            }
        }
    }
}
