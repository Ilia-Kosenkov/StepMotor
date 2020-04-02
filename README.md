# StepMotor
A library to handle Trinamic stepper motors

This set of APIs allows one to communicate through COM port with a stepper motor. The library is designed around asynchronous execution. 
The `IAsyncMotor` interface represents commonly used commands (like `MoveToPosition`, `WaitForPositionReached`, etc), and can be obtained from the `IAsyncMotorFactory`.
Due to historical reasons (more than one test implementation of stepper motor handler class), the class that implements `IAsyncMOtorFactory` contains a generic parameter of type `StepMotor`, which is an abstract base class for a set of experimental implementations.
Out of all versions the `SynchronizedMotor` was selected.

## How to use
Create a instance of `IAsyncMotorFactory` using `new StepMotorProvider<SynchronizedMotor>(ILogger?)`.
Use factory to spawn `IAsyncMotor` off of a specific `SerialPort` at a specific `Address`.

The motor can then be used as follows:
```csharp
const int step = 1500;
using var _port = new SerialPort(@"COM1");
await using var _motor = await (new StepMotorProvider<SynchronizedMotor>().TryCreateFirstAsync(_port));
await _motor.ReturnToOriginAsync();

for(var i = 0; i < 10; i++)
{
  var currentPos = await _motor.GetPositionAsync();
  await _motor.MoveToPositionAsync(currentPos + step);
}

await _motor.ReferenceReturnToOriginAsync();
```
