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
    

# send email form from_addr to to_addr through smtp_server 'smtp.gmail.com:587'.
#!/usr/bin/env python3
# -*- coding: utf-8 -*-

from email import encoders
from email.header import Header
from email.mime.text import MIMEText
from email.utils import parseaddr, formataddr
import smtplib

def _format_addr(s):
        name, addr = parseaddr(s)
        return formataddr((Header(name, 'utf-8').encode(), addr))

from_addr = input('From: ')
# from_addr = raw_input('From: ') for python2
password = input('Password: ')
to_addr = input('To: ')
smtp_server = input('SMTP server: ')

msg = MIMEText('hello, send by Python 6...', 'plain', 'utf-8')
msg['From'] =_format_addr('Python lover <%s>' % from_addr)
msg['To'] = _format_addr('administrator <%s>' % to_addr)
msg['Subject'] = Header('hello from smtp server 6', 'utf-8').encode()

server = smtplib.SMTP(smtp_server,587)
# server = smtplib.SMTP('smtp.gmail.com:587')
server.starttls()
server.set_debuglevel(1)
server.login(from_addr, password)
server.sendmail(from_addr, [to_addr], msg.as_string())
server.quit()

# send email from from_addr to to_addr through smtp_server 'smtp.gmail.com:587' with attachment G:\\old_machine\\timeSchedule.jpg.
#!/usr/bin/env python3
# -*- coding: utf-8 -*-

from email import encoders
from email.header import Header
from email.mime.text import MIMEText
from email.utils import parseaddr, formataddr

from email.mime.multipart import MIMEMultipart
from email.mime.base import MIMEBase

import smtplib

def _format_addr(s):
        name, addr = parseaddr(s)
        return formataddr((Header(name, 'utf-8').encode(), addr))

from_addr = input('From: ')
password = input('Password: ')
to_addr = input('To: ')
smtp_server = input('SMTP server: ')

msg = MIMEMultipart()

msg['From'] =_format_addr('Python lover <%s>' % from_addr)
msg['To'] = _format_addr('administrator <%s>' % to_addr)
msg['Subject'] = Header('hello from smtp server 7', 'utf-8').encode()

msg.attach(MIMEText('send with file...', 'plain', 'utf-8'))
with open('G:\\old_machine\\timeSchedule.jpg', 'rb') as f:
    # set file name and type jpg:
    mime = MIMEBase('image', 'jpg', filename='timeSchedule.jpg')
    # add header info:
    mime.add_header('Content-Disposition', 'attachment', filename='timeSchedule.jpg')
    mime.add_header('Content-ID', '<0>')
    mime.add_header('X-Attachment-Id', '0')
    # read the attachment:
    mime.set_payload(f.read())
    # Base64 :
    encoders.encode_base64(mime)
    # add to MIMEMultipart:
    msg.attach(mime)

server = smtplib.SMTP(smtp_server,587)
# server = smtplib.SMTP('smtp.gmail.com:587')
server.starttls()
server.set_debuglevel(1)
server.login(from_addr, password)
server.sendmail(from_addr, [to_addr], msg.as_string())
server.quit()


