#include <unistd.h>
#include <signal.h>
#include<stdio.h>
#include<stdlib.h>
#include<string.h>
#include<errno.h>
#include<sys/types.h>
#include<sys/socket.h>
#include<netinet/in.h>
#include<regex.h>
#define MAXLINE 4096

void initData(char *f1,char *f2) {
    int    i=0,j=0;
    while(f1[i]!='\0') {
        if (f1[i] == '\\' && f1[i+1] == 'n')
            f2[j++] = '\n',i++;
        else if (f1[i] == '\\' && f1[i+1] == '"')
            f2[j++] = '\"',i++;
        else
            f2[j++] = f1[i];
        i++;
    }
}

void stop(int signo) {
    printf("exit now\n");
    exit(0);
}

int main(int argc, char *argv[]) {
    int    listenfd, connfd, n, m;
    struct sockaddr_in     servaddr;
    char   str[MAXLINE];
    char   str0[MAXLINE];
    char   buff[MAXLINE];
    char   *answer;
    FILE   *fp,*fp0;
    char   fileContent1[MAXLINE];
    if( argc != 4) {
        printf("usage: ./SPtrace <port> <expect_file> <response_file>\n");
        exit(0);
    }

    if( (listenfd = socket(AF_INET, SOCK_STREAM, 0)) == -1 ) {
        printf("create socket error: %s(errno: %d)\n",strerror(errno),errno);
        exit(0);
    }

    memset(&servaddr, 0, sizeof(servaddr));
    servaddr.sin_family = AF_INET;
    servaddr.sin_addr.s_addr = htonl(INADDR_ANY);
    servaddr.sin_port = htons(atoi(argv[1]));

    if( bind(listenfd, (struct sockaddr*)&servaddr, sizeof(servaddr)) == -1) {
        printf("bind socket error: %s(errno: %d)\n",strerror(errno),errno);
        exit(0);
    }

    if( listen(listenfd, 10) == -1) {
        printf("listen socket error: %s(errno: %d)\n",strerror(errno),errno);
        exit(0);
    }

  while(1){
    printf("======waiting for client's request======\n");
    signal(SIGINT,stop);
    if( (connfd = accept(listenfd, (struct sockaddr*)NULL, NULL)) == -1) {
        printf("accept socket error: %s(errno: %d)",strerror(errno),errno);
        exit(0);
    }

    if( (fp=fopen(argv[2],"rt"))==NULL) {
        printf("Cannot open file %s!",argv[2]);
        exit(1);
     }

    if( (fp0=fopen(argv[3],"rt"))==NULL) {
        printf("Cannot open file %s!",argv[3]);
        exit(1);
     }


     while(fgets(str,MAXLINE,fp)) {
         fgets(str0,MAXLINE,fp0);
         initData(str0,fileContent1);
         printf("%s",str);
         if(strcmp(str,"null\n")==0) {
             answer = fileContent1;
             printf("send the answer:\n%s",answer);
             if( send(connfd,answer,strlen(answer), 0) < 0) {
                 printf("send msg error: %s(errno: %d)\n", strerror(errno), errno);
                 exit(0);
             } 
             memset(fileContent1,0,MAXLINE);
             continue;
         }
         m=1;
         while(m) {
             n = recv(connfd, buff, MAXLINE, 0);
             buff[n] = '\0';

             int status;
             int cflags = REG_EXTENDED;
             regmatch_t pmatch[1];
             const size_t nmatch = 1;
             regex_t reg;
             const char * pattern;
             printf("recv msg from client: %s", buff);
             str[strlen(str)-1]='\0';
             pattern = str;
             regcomp(&reg,pattern,cflags);
             status = regexec(&reg,buff,nmatch,pmatch,0);
             if (status == 0) {
                 answer = fileContent1;
                 printf("send the answer:\n%s",answer);
                 if( send(connfd,answer,strlen(answer), 0) < 0) {
                     printf("send msg error: %s(errno: %d)\n", strerror(errno), errno);
                     exit(0);
                 }

                 memset(fileContent1,0,MAXLINE);
                 m=0;
             }
         }
     }
   close(connfd);
   fclose(fp);
   fclose(fp0);
 }
close(listenfd);
}
