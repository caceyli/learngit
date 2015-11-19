package myjava;

/**
JDBC编程步骤：
----java与Oracle数据的连接（lomboz_eclipse环境下）
  1.在Oracle数据库安装文件夹中找到jdbc文件夹→lib文件夹→classesl2.jar
  2.lomboz_eclipse中导入此Jar包, 导入方法：
    建立一个项目，在项目名称上右键鼠标选择Build Path→Add External Archives→选择classesl2.jar进行导入

  3.新建一程序编写与Oracle连接的代码, 步骤如下：
    1.实例话驱动类
      class.forName("Oracle.jdbc.driver.OracleDriver");
    2.建立到数据库的连接
      Connection conn = DriverManager.getConnection("jdbc:oracle:thin:@192.168.8.1:1521:yuewei","scott","tiger");
    3.将数据发送到数据库中
      Statement stm = conn.CreatStatement();
    4.执行语句（select语句）
      ResultSet rs = stm.executeQuery(select * from dept);
    5.显示语句
      rs.getString("deptno");

[oracle@VMDC8245 oracle]$ find ./ -name classes*
./u01/app/oracle/product/11.2.0/javavm/admin/classes.bin
./u01/app/oracle/product/11.2.0/oui/jlib/classes12.jar

**/

import java.sql.*;  

public class TestJDBC {  
  
 public static void main(String[] args) {  
  ResultSet rs = null;  
  Statement stmt = null;  
  Connection conn = null;  
  try {  
   Class.forName("oracle.jdbc.driver.OracleDriver");  
   //new oracle.jdbc.driver.OracleDriver();  
   conn = DriverManager.getConnection("jdbc:oracle:thin:@192.168.8.245:1521:ora11g", "system", "bdnacn");  
   stmt = conn.createStatement();  
   rs = stmt.executeQuery("select * from dba_users");  
   while(rs.next()) {  
    System.out.println(rs.getString("username"));  
    //System.out.println(rs.getInt("deptno"));  
   }  
  } catch (ClassNotFoundException e) {  
   e.printStackTrace();  
  } catch (SQLException e) {  
   e.printStackTrace();  
  } finally {  
   try {  
    if(rs != null) {  
     rs.close();  
     rs = null;  
    }  
    if(stmt != null) {  
     stmt.close();  
     stmt = null;  
    }  
    if(conn != null) {  
     conn.close();  
     conn = null;  
    }  
   } catch (SQLException e) {  
    e.printStackTrace();  
   }  
  }  
 }  
  
}  