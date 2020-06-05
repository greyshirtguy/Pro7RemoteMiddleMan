# Pro7RemoteMiddleMan
A "Middle-Man" Windows application to provide simple Master-Slave functionality with ProPresenter 7

Currently a proof-of-concept! - Many things are hard-coded without UI to update and it's probably super buggy!

I just wanted to test the idea of creating a middle-man application to create a Master-Slave setup with ProPresenter 7.
It uses the mobile App remote protocol to connect to two separate Pro7 machines anywhere on your network via websockets.
It listens for notifications of slides being triggered in the "master" Pro7 and sends trigger commands for same presentation/slide to the "slave" Pro7.

Note: not all master-slave functionality is possble with the remote protocol (eg - there are no notification messages sent from master for any clear actions)

This project was made in Visual Studio 2019 Community edition and uses WebSocket4Net pacakge by Kerry Jiang (NuGet Package)
