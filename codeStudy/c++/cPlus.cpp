/*server.cpp write message to client, waiting for it reading on 192.168.8.245*/
#include <stdio.h>
#include <string.h>
#include <unistd.h>
#include <arpa/inet.h>
#include <sys/socket.h>
#include <netinet/in.h>
int main(){

    int serv_sock = socket(AF_INET, SOCK_STREAM, IPPROTO_TCP);

    struct sockaddr_in serv_addr;
    memset(&serv_addr, 0, sizeof(serv_addr));
    serv_addr.sin_family = AF_INET;
    serv_addr.sin_addr.s_addr = inet_addr("192.168.8.245");
    serv_addr.sin_port = htons(1234);

    bind(serv_sock, (struct sockaddr*)&serv_addr, sizeof(serv_addr));

    listen(serv_sock, 20);

    struct sockaddr_in clnt_addr;
    socklen_t clnt_addr_size = sizeof(clnt_addr);
    int clnt_sock = accept(serv_sock, (struct sockaddr*)&clnt_addr, &clnt_addr_size);

    char str[] = "Hello World!";
    write(clnt_sock, str, sizeof(str));

    close(clnt_sock);
    close(serv_sock);
    return 0;
}


/*client.cpp read messsage from server on 192.168.9.102*/
#include <stdio.h>
#include <string.h>
#include <stdlib.h>
#include <unistd.h>
#include <arpa/inet.h>
#include <sys/socket.h>
int main(){

    int sock = socket(AF_INET, SOCK_STREAM, 0);

    struct sockaddr_in serv_addr;
    memset(&serv_addr, 0, sizeof(serv_addr));
    serv_addr.sin_family = AF_INET;
    serv_addr.sin_addr.s_addr = inet_addr("192.168.8.245");
    serv_addr.sin_port = htons(1234);
    connect(sock, (struct sockaddr*)&serv_addr, sizeof(serv_addr));


    char buffer[40];
    read(sock, buffer, sizeof(buffer)-1);

    printf("Message form server: %s\n", buffer);

    close(sock);
    return 0;
}

/*
1.
先编译 server.cpp 并运行：
[admin@localhost ~]$ g++ server.cpp -o server
[admin@localhost ~]$ ./server
|
正常情况下，程序运行到 accept() 函数就会被阻塞，等待客户端发起请求。

接下来编译 client.cpp 并运行：
[admin@localhost ~]$ g++ client.cpp -o client
[admin@localhost ~]$ ./client
Message form server: Hello World!
[admin@localhost ~]$
client 运行后，通过 connect() 函数向 server 发起请求，处于监听状态的 server 被激活，执行 accept() 函数，接受客户端的请求，然后执行 write() 函数向 client 传回数据。client 接收到传回的数据后，connect() 就运行结束了，然后使用 read() 将数据读取出来。

需要注意的是：
1) server 只接受一次 client 请求，当 server 向 client 传回数据后，程序就运行结束了。如果想再次接收到服务器的数据，必须再次运行 server，所以这是一个非常简陋的 socket 程序，不能够一直接受客户端的请求。

2) 上面的源文件后缀为.cpp，是C++代码，所以要用g++命令来编译。
C++和C语言的一个重要区别是：在C语言中，变量必须在函数的开头定义；而在C++中，变量可以在函数的任何地方定义，使用更加灵活。这里之所以使用C++代码，是不希望在函数开头堆砌过多变量。



2. 服务器进程中系统调用的顺序
          socket（）————bind（）————listen（）————accept（）
在面向连接的协议的程序中,服务器执行以下函数：
l 调用socket()函数创建一个套接字。
l 调用bind()函数把自己绑定在一个地址上。
l 调用listen()函数侦听连接。
l 调用accept()函数接受所有引入的请求。
l 调用recv()函数获取引入的信息然后调用send()回答。
 
     TCP三次握手协议：
（1）客户端先用connect（）向服务器发出一个要求连接的信号SYN1。
（2）服务器进程接收到这个信号后，发回应答信号ack1，同时这也是一个要求回答的信号SYN2。
（3）客户端收到应答信号ack1和SYN2后，再次应答ack2。
（4）服务器收到应答信号ack2，一次连接才算建立完成。


Reference:

http://www.cnblogs.com/zhangjing0502/archive/2012/06/26/2564001.html
http://c.biancheng.net/cpp/html/3030.html
*/