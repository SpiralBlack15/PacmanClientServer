# PacmanClientServer
Just small test pacman client-server. No ghost or bonuses included yet.

/Build folder contains builds of the server and the client app. Server is console C# application, client is 
Unity graphic application with 640x480 window. You can run multiple clients if they have different addresses.
Simple WASD/Arrows movement. If server falls, client will stop the game and wait for reconnect. When reconnected,
the game resume. In current implementation there are no any save data excepts map-file, so after recovery server 
will take last client position. Each new client have their own game desk.

Network code is just my WTF implementation on async-awaits, threads and some esoteric questionable solutions. Well,
in any event?.Invoke() [haha] it was fun to make it in just one week at evenings after work-time. Something like
speed-coding.
