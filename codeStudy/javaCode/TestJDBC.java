package myjava;

/**
JDBC��̲��裺
----java��Oracle���ݵ����ӣ�lomboz_eclipse�����£�
  1.��Oracle���ݿⰲװ�ļ������ҵ�jdbc�ļ��С�lib�ļ��С�classesl2.jar
  2.lomboz_eclipse�е����Jar��, ���뷽����
    ����һ����Ŀ������Ŀ�������Ҽ����ѡ��Build Path��Add External Archives��ѡ��classesl2.jar���е���

  3.�½�һ�����д��Oracle���ӵĴ���, �������£�
    1.ʵ����������
      class.forName("Oracle.jdbc.driver.OracleDriver");
    2.���������ݿ������
      Connection conn = DriverManager.getConnection("jdbc:oracle:thin:@192.168.8.1:1521:yuewei","scott","tiger");
    3.�����ݷ��͵����ݿ���
      Statement stm = conn.CreatStatement();
    4.ִ����䣨select��䣩
      ResultSet rs = stm.executeQuery(select * from dept);
    5.��ʾ���
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