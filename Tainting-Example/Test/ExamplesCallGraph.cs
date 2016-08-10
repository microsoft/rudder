// Copyright (c) Edgardo Zoppi.  All Rights Reserved.  Licensed under the MIT License.  See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Test
{
	interface IPrintable
	{
		void Print();
	}

	abstract class Shape : IPrintable
	{
		public void Show()
		{
		}

		public virtual void Draw()
		{
		}

		public virtual void Print()
		{
		}

        public virtual void DoSomething(int p)
        {

        }

	}

	class Ellipse : Shape
	{
		public override void Draw()
		{
		}
	}

	class Circle : Ellipse
	{
		public override void Print()
		{
		}
        public object Self(object p)
        {
            SinkClass.SinkMethod(p);
            return p;
        }

	}

	class Rectangle : Shape
	{
		public override void Print()
		{
		}
	}

	class Square : Rectangle
	{
		public override void Draw()
		{
            DoSomething(10);
		}
        public override void DoSomething(int p)
        {
            SinkClass.SinkMethod(p);
        }
    }

	class ExamplesCallGraph
	{
        object field;
        object otherField;
        object moreFields;
        delegate void DoSomethingDelegate(int p);


        public void Example1()
		{
            var mySecret1 = (int)SourceClass.SourceMethod(1);
            var mySecret2 = (int)SourceClass.SourceMethod(2);
            var mySecret3 = SourceClass.SourceMethod("S3");
            var mySecret4 = SourceClass.SourceMethod("S4");
            var mySecret5 = SourceClass.SourceMethod("S5");
            var mySecret6 = (int)SourceClass.SourceMethod(6);
            var mySecret7 = (int)SourceClass.SourceMethod(7);

            Rectangle squareRect = new Square();
            // Leak: flow through delegate Square
            DoSomethingDelegate d = squareRect.DoSomething;
            d(mySecret1);

            Rectangle realRect= new Rectangle();
            // No Leak: flow through delegate rectangle
            DoSomethingDelegate d2= realRect.DoSomething;
            d2(mySecret2);

            // Leak: direct 
            SinkClass.SinkMethod(mySecret3);

            // Leak: through Circle.Self
            var newCircle = new Circle();
            var mySelf = newCircle.Self(mySecret4);

            // Leak: through field
            this.field = mySecret5;
            SinkClass.SinkMethod(this.field);
            // Leak: through otherfield
            this.otherField = this.field;
            SinkClass.SinkMethod(otherField);
            // no leak here
            this.moreFields = null;
            SinkClass.SinkMethod(moreFields);

            // No leak
            Shape circle = new Circle();
            circle.Show(); // Shape.Show
            circle.Draw(); // Ellipse.Draw
            circle.Print(); // Circle.Print


            Rectangle square = new Square();
            square.Show(); // Shape.Show
            square.Draw(); // Square.Draw
            square.Print(); // Rectangle.Print
            // Leak: in square
            square.DoSomething(mySecret6);
            // no Leak in rectangle
            realRect.DoSomething(mySecret7);


        }
    }

    public class SinkClass
    {
        public static void SinkMethod(object p)
        {

        }
    }
    public class SourceClass
    {
        public static object SourceMethod(object p)
        {
            return p;
        }
    }
}
