/**Die Klasse erzeugt die Grafik eines über eine Klasenvariable SIZE  skalierbares Quadrat*/
public class VierQuadrate {
    /**Gibt die Kantenlänge des vierQuadrates an*/
    public static final int SIZE= 3;
    
    
    public static void main(String[] args){
        vierQuadrate();        
    }
    
    /**Setzt die Grafik aus den Einzelteilen zusammen*/
    public static void vierQuadrate(){
        printAllHbord();
        for (int l=1; l <= 2; l++){
            for (int h =1; h <= SIZE; h++){
                printAllVBorder();
            }
            printAllHbord();
        }
    }
    /**Vereinigt die Horizontalen Kanten*/
    public static void printAllHbord(){
        printHborder();
        System.out.print("+");
        printHborder();
        System.out.println();
    }
    /**skaliert die die Horizontale einzelne Kante*/
    public static void printHborder(){
        for (int k=1; k <= SIZE +1; k++){
            System.out.print("=");
        }
    }
    /**Kombinniert alle Außenkanten (vertikal) */
    public static void printAllVBorder(){
        printVborder();
        printSpaces();
        System.out.print("|");
        printSpaces();
        printVborder();
        System.out.println();
    }
    
    /**erstellt die Skalierbare Leere*/
    public static void printSpaces(){
        for (int l=1; l <= SIZE; l++){
            System.out.print(" ");
        }
    }
    /**erzeugt die sichtbare Außenkante (vertikal)*/
    public static void printVborder(){
        System.out.print("#");
    }
}