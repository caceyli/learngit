# createFileDemo.py: create a new file 'test,txt', and write 2 lines into that file. If that file alreadey existed, it will be covered.
def main():
	f=open("test.txt",'w+')
	f.writelines(['dd\n','ee'])
	f.close()

if __name__ == '__main__':
	main()


# readOutDemo.py: read every line from file 'test.txt' and out put all lines on screen.
def main():
	f=open("test.txt")
	line=f.readline()
	while line:
		print line,                # with ',' to ignore '\n' for python 2.X
		#print(line, end = '')     # ignore '\n' for python 3.X
		line=f.readline()
	f.close()

if __name__ == '__main__':
	main()


# ifInputDemo.py: get input from screen, and change string to int so that it can be math.
age = input('input your age：')
yourAge = int(age)
if yourAge >= 18:
    print('your age is', age)
    print('adult')
else:
    print('teenager')    
    


