from __future__ import annotations
"""
Point and Rectangle classes.

From: https://wiki.python.org/moin/PointsAndRectangles

This code is in the public domain.

Size   -- size with (width, height)
Point  -- point with (x,y) coordinates
Rect   -- two points, forming a rectangle
"""

import math
from typing import Tuple


class Size:
    
    """A size identified by (width, height) values.

       Added by chris@codeproject.com 1Feb2023
       This class is also Public Domain
    
    supports: +, -, *, /, str, repr
    
    length       -- calculate length of vector to point from origin
    as_tuple     -- construct tuple (width,height)
    clone        -- construct a duplicate
    integerize   -- convert width & height to integers
    floatize     -- convert width & height to floats
    """
    
    def __init__(self, width: float=0.0, height:float=0.0) -> None:
        self.width = width
        self.height = height
    
    def __add__(self, p: Size) -> Size:
        """Size(width1+width2, height1+height2)"""
        return Size(self.width+p.width, self.height+p.height)
    
    def __sub__(self, p: Size) -> Size:
        """Size(width1-width2, height1-height2)"""
        return Size(self.width-p.width, self.height-p.height)
    
    def __mul__( self, scalar: float) -> Size:
        """Size(width1*scalar, height1*scalar)"""
        return Size(self.width*scalar, self.height*scalar)

    def __rmul__( self, scalar: float) -> Size:
        """Size(width1*scalar, height1*scalar)"""
        return Size(self.width*scalar, self.height*scalar)

    def __div__(self, scalar: float) -> Size:
        """Size(width1/scalar, height1/scalar)"""
        return Size(self.width/scalar, self.height/scalar)

    def scale(self, scale: float) -> None:
        self.width  *= scale
        self.height *= scale

    def length(self) -> float:
        return math.sqrt(self.width**2 + self.height**2)
       
    def as_tuple(self) -> Tuple[float, float]:
        """(width, height)"""
        return (self.width, self.height)
    
    def clone(self) -> Size:
        """Return a full copy of this object."""
        return Size(self.width, self.height)
    
    def integerize(self) -> None:
        """Convert values to integers."""
        self.width  = int(self.width)
        self.height = int(self.height)
    
    def floatize(self) -> None:
        """Convert values to floats."""
        self.width  = float(self.width)
        self.height = float(self.height)

    def __str__(self) -> str:
        return f"<Size ({self.wifth},{self.height})>"
    
    def __repr__(self) -> str:
        return "%s(%r, %r)" % (self.__class__.__name__, self.width, self.height)


class Point:
    
    """A point identified by (x,y) coordinates.
    
    supports: +, -, *, /, str, repr
    
    length       -- calculate length of vector to point from origin
    distance_to  -- calculate distance between two points
    as_tuple     -- construct tuple (x,y)
    clone        -- construct a duplicate
    integerize   -- convert x & y to integers
    floatize     -- convert x & y to floats
    move_to      -- reset x & y
    slide        -- move (in place) +dx, +dy, as spec'd by point
    slide_xy     -- move (in place) +dx, +dy
    rotate       -- rotate around the origin
    rotate_about -- rotate around another point
    """
    
    def __init__(self, x=0.0, y=0.0) -> None:
        self.x = x
        self.y = y
    
    def __add__(self, p) -> Point:
        """Point(x1+x2, y1+y2)"""
        return Point(self.x+p.x, self.y+p.y)
    
    def __sub__(self, p) -> Point:
        """Point(x1-x2, y1-y2)"""
        return Point(self.x-p.x, self.y-p.y)
    
    def __mul__( self, scalar: float) -> Point:
        """Point(x1*scalar, y1*scalar)"""
        return Point(self.x*scalar, self.y*scalar)
    
    def __div__(self, scalar: float) -> Point:
        """Point(x1/scalar, y1/scalar)"""
        return Point(self.x/scalar, self.y/scalar)
    
    def __str__(self) -> str:
        return "(%s, %s)" % (self.x, self.y)
    
    def __repr__(self) -> str:
        return "%s(%r, %r)" % (self.__class__.__name__, self.x, self.y)
    
    def length(self) -> float:
        return math.sqrt(self.x**2 + self.y**2)
    
    def distance_to(self, p: Point) -> float:
        """Calculate the distance between two points."""
        return (self - p).length()
    
    def as_tuple(self) -> Tuple[float, float]:
        """(x, y)"""
        return (self.x, self.y)
    
    def clone(self) -> Point:
        """Return a full copy of this point."""
        return Point(self.x, self.y)
    
    def integerize(self) -> None:
        """Convert co-ordinate values to integers."""
        self.x = int(self.x)
        self.y = int(self.y)
    
    def floatize(self) -> None:
        """Convert co-ordinate values to floats."""
        self.x = float(self.x)
        self.y = float(self.y)
    
    def move_to(self, x, y) -> None:
        """Reset x & y coordinates."""
        self.x = x
        self.y = y
    
    def slide(self, p) -> None:
        '''Move to new (x+dx,y+dy).
        
        Can anyone think up a better name for this function?
        slide? shift? delta? move_by?
        '''
        self.x = self.x + p.x
        self.y = self.y + p.y
    
    def slide_xy(self, dx, dy) -> None:
        '''Move to new (x+dx,y+dy).
        
        Can anyone think up a better name for this function?
        slide? shift? delta? move_by?
        '''
        self.x = self.x + dx
        self.y = self.y + dy
    
    def rotate(self, rad: float) -> Point:
        """Rotate counter-clockwise by rad radians.
        
        Positive y goes *up,* as in traditional mathematics.
        
        Interestingly, you can use this in y-down computer graphics, if
        you just remember that it turns clockwise, rather than
        counter-clockwise.
        
        The new position is returned as a new Point.
        """
        s, c = [f(rad) for f in (math.sin, math.cos)]
        x, y = (c*self.x - s*self.y, s*self.x + c*self.y)
        return Point(x,y)
    
    def rotate_about(self, p: Point, theta: float) -> Point:
        """Rotate counter-clockwise around a point, by theta degrees.
        
        Positive y goes *up,* as in traditional mathematics.
        
        The new position is returned as a new Point.
        """
        result = self.clone()
        result.slide(-p.x, -p.y)
        result.rotate(theta)
        result.slide(p.x, p.y)
        return result

    def __str__(self) -> str:
        return f"<Point ({self.x},{self.y})>"
    
    def __repr__(self) -> str:
        return "%s(%r, %r)" % (self.__class__.__name__, self.x, self.y)


class Rect:

    """A rectangle identified by two points.

    The rectangle stores left, top, right, and bottom values.

    Coordinates are based on screen coordinates.

    origin                               top
       +-----> x increases                |
       |                           left  -+-  right
       v                                  |
    y increases                         bottom

    set_points     -- reset rectangle coordinates
    contains_point -- is a point inside?
    overlaps       -- does a rectangle overlap?
    top_left       -- get top-left corner
    bottom_right   -- get bottom-right corner
    expanded_by    -- grow (or shrink)
    """

    def __init__(self, *args) -> None:
 
        """Initialize a rectangle from 1 rect, 2 points or 4 bounds."""

        if len(args) == 1 and isinstance(args[0], Rect):
            self.set_points(args[0].top_left(), args[0].bottom_right())

        elif len(args) == 2 and isinstance(args[0], Point):
            self.set_points(args[0], args[1])

        elif len(args) == 4 and (isinstance(args[0], float) or isinstance(args[0], int)):
            self.set_bounds(args[0], args[1], args[2], args[3])


    def set_bounds(self, left: float, top: float, right: float, bottom: float) -> None:
        """Reset the rectangle coordinates."""
        self.left   = min(left, right)
        self.top    = min(top, bottom)
        self.right  = max(left, right)
        self.bottom = max(top, bottom)

    def set_points(self, pt1: Point, pt2: Point) -> None:
        """Reset the rectangle coordinates."""
        (x1, y1) = pt1.as_tuple()
        (x2, y2) = pt2.as_tuple()
        self.set_bounds(x1, y1, x2, y2)

    def contains_point(self, pt: Point) -> bool:
        """Return true if a point is inside the rectangle."""
        return (self.left <= pt.x <= self.right and
                self.top  <= pt.y <= self.bottom)

    def contains(self, other: Rect, can_touch: bool = True) -> bool:
        """Return true if a rectangle is inside the rectangle."""
        if can_touch:
            return (other.left  >= self.left  and other.top    >= self.top  and
                    other.right <= self.right and other.bottom <= self.bottom)

        return (other.left  > self.left  and other.top    > self.top  and
                other.right < self.right and other.bottom < self.bottom)

    def overlaps(self, other: Rect) -> bool:
        """Return true if a rectangle overlaps this rectangle."""
        return (self.right > other.left and self.left < other.right and
                self.top < other.bottom and self.bottom > other.top)
    
    def top_left(self):
        """Return the top-left corner as a Point."""
        return Point(self.left, self.top)
    
    def bottom_right(self) -> Point:
        """Return the bottom-right corner as a Point."""
        return Point(self.right, self.bottom)
    
    def expanded_by(self, n: float) -> Rect:
        """Return a rectangle with extended borders.

        Create a new rectangle that is wider and taller than the
        immediate one. All sides are extended by "n" points.
        """
        return Rect(self.left - n, self.top - n, self.right + n, self.bottom + n)
    
    def clone(self, rect: Rect) -> Rect:
        """Return a full copy of this point."""
        return Rect(rect.top_left(), rect.bottom_right())

    def integerize(self) -> None:
        """Convert values to integers."""
        self.left   = int(self.left)
        self.top    = int(self.top)
        self.right  = int(self.right)
        self.bottom = int(self.bottom)
    
    def floatize(self) -> None:
        """Convert values to floats."""
        self.width = float(self.width)
        self.height = float(self.height)

    def __str__(self) -> str:
        return f"<Rect ({self.left},{self.top})-({self.right},{self.bottom})>"
    
    def __repr__(self) -> str:
        return "%s(%r, %r)" % (self.__class__.__name__,
                               Point(self.left, self.top),
                               Point(self.right, self.bottom))