#include<stdio.h>
main()
{
  int i,j,s;
  for(i=1;i<10;i++){
     for(j=1;j<=i;j++){
        s=j*i;
        printf("%d*%d=%-3d",j,i,s);
     }
     printf("\n");
  }
}


1*1=1
1*2=2  2*2=4
1*3=3  2*3=6  3*3=9
1*4=4  2*4=8  3*4=12 4*4=16
1*5=5  2*5=10 3*5=15 4*5=20 5*5=25
1*6=6  2*6=12 3*6=18 4*6=24 5*6=30 6*6=36
1*7=7  2*7=14 3*7=21 4*7=28 5*7=35 6*7=42 7*7=49
1*8=8  2*8=16 3*8=24 4*8=32 5*8=40 6*8=48 7*8=56 8*8=64
1*9=9  2*9=18 3*9=27 4*9=36 5*9=45 6*9=54 7*9=63 8*9=72 9*9=81


/*open file nine.c and read all its content to screen*/
#include<stdio.h>
main()
{
  FILE *fp;
  char str[110];
  if((fp=fopen("nine.c","rb"))==NULL)
  {
     printf("can't open file nini.c!");
     exit(1);
   }
  while(fgets(str,110,fp))
  {
     printf("%s",str);
  }
  fclose(fp);
}


/*open file nine.c, add a string at its end and read all its content to screen*/
#include<stdio.h>
main()
{
  FILE *fp;
  char str[110];
  if((fp=fopen("nine.c","at+"))==NULL)
	{
	  printf ("can't open file nine.c!");
	  exit(1);
	}
  printf("input a string:\n");
  scanf("%s",str);
  fputs(str,fp);
  rewind(fp);
  while(fgets(str,110,fp))
    {
      printf("%s",str);
    }
  fclose(fp);
}


#include<stdio.h>
void bubbleSorted(int a[],int n)
{
    int i,j,t,inc=0,flag=1;
        while(flag)
        {
            inc++;
            flag=0;
                for(i=0;i<n-inc;i++)
                {
                    if(a[i]>a[i+1])
                    {
                        t=a[i];
                        a[i]=a[i+1];
                        a[i+1]=t;
                        flag=1;
                    }
                }
        }
}

main()
{
    int a[10],n=10,i;
    printf("please in put %d numbers:\n",n);
    for(i=0;i<10;i++)
    {
        scanf("%d",&a[i]);
    }
    bubbleSorted(a,10);
    for(i=0;i<10;i++)
    {
        printf("%d ",a[i]);
    }
}

    

/*list sorted by inserting smaller number*/	
#include<stdio.h>

void dirInsert(int a[],int n)
{
        int i,j;
        for (i=2;i<=n;i++)
        {
                a[0]=a[i];
                j=i-1;
                while(a[j]>a[0])
                {
                        a[j+1]=a[j];
                        j--;
                }
                a[j+1]=a[0];


        }
}

main()
{
    int a[10],n=10,i;
    printf("please input %d numbers:\n",n);
    for(i=1;i<=10;i++)
    {
        scanf("%d",&a[i]);
    }
    dirInsert(a,10);
    for(i=1;i<=10;i++)
    {
        printf("%d ",a[i]);
    }
}


